using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sentinela.Shared.Core.Interfaces;
using Sentinela.Shared.Messaging.Events;

namespace Sentinela.Correlation.Engine;

public class CorrelationBackgroundService : BackgroundService
{
    private readonly ICorrelationEngine _engine;
    private readonly IEventBus _eventBus;
    private readonly ILogger<CorrelationBackgroundService> _logger;
    private readonly CorrelationOptions _options;

    public CorrelationBackgroundService(
        ICorrelationEngine engine,
        IEventBus eventBus,
        IOptions<CorrelationOptions> options,
        ILogger<CorrelationBackgroundService> logger)
    {
        _engine = engine;
        _eventBus = eventBus;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Correlation background service started");

        await _eventBus.SubscribeAsync<SecurityEventReceivedEvent>(async (message, ct) =>
        {
            try
            {
                _logger.LogDebug("Processing correlation event: {EventType}", message.EventType);

                var result = await _engine.AnalyzeEventAsync(message.Payload);

                if (result != null)
                {
                    _logger.LogInformation(
                        "Correlation detected: {PatternName} (Score: {Score}) for computer {ComputerId}",
                        result.PatternName, result.Score, result.ComputerId);

                    await _eventBus.PublishAsync(new CorrelationAlertEvent
                    {
                        Alert = new Sentinela.Shared.Domain.Security.SecurityAlert(
                            $"Correlation: {result.PatternName}",
                            result.Description,
                            result.Severity,
                            "Correlation",
                            "CorrelationEngine",
                            result.ComputerId),
                        RelatedEventIds = result.RelatedEvents,
                        Score = result.Score,
                        Tags = result.Tags
                    }, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing correlation event");
            }
        }, stoppingToken);

        var timerTask = RunPeriodicAnalysis(stoppingToken);
        await timerTask;
    }

    private async Task RunPeriodicAnalysis(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(_options.DefaultTimeWindowMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);

                _logger.LogDebug("Running periodic correlation analysis");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in periodic correlation analysis");
            }
        }
    }
}

public class SecurityEventReceivedEvent : IEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(SecurityEventReceivedEvent);
    public string Source => "Sentinela.Agent";

    public object Payload { get; init; } = null!;
}
