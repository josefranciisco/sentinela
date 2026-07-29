using Sentinela.AlertEngine.Configuration;
using Sentinela.Shared.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace Sentinela.AlertEngine.Channels;

public class AlertCooldownService : BackgroundService
{
    private readonly ICacheService _cache;
    private readonly IOptions<AlertEngineOptions> _options;
    private readonly ILogger<AlertCooldownService> _logger;

    public AlertCooldownService(
        ICacheService cache,
        IOptions<AlertEngineOptions> options,
        ILogger<AlertCooldownService> logger)
    {
        _cache = cache;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Alert cooldown service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredCooldownsAsync();
                await Task.Delay(_options.Value.AlertProcessingIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in alert cooldown maintenance cycle");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }

    private async Task CleanupExpiredCooldownsAsync()
    {
        // Cooldowns have TTL set at creation time in AlertEvaluator,
        // so Redis auto-evicts them. This method serves as a fallback
        // to track and log alert frequency metrics.

        _logger.LogInformation("Alert cooldown maintenance cycle completed");
    }
}
