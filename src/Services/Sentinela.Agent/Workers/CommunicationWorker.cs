using Microsoft.Extensions.Options;
using Sentinela.Agent.Configuration;
using Sentinela.Agent.Core.Health;
using Sentinela.Agent.Services;

namespace Sentinela.Agent.Workers;

public class CommunicationWorker : BackgroundService
{
    private readonly IAgentHubClient _hubClient;
    private readonly ICommunicationService _communication;
    private readonly IOfflineCacheService _cache;
    private readonly ICommandService? _commandService;
    private readonly IConfigurationService? _configService;
    private readonly IAgentStateService _state;
    private readonly IAgentHealthService? _healthService;
    private readonly ServerConnectionOptions _options;
    private readonly ILogger<CommunicationWorker> _logger;

    public CommunicationWorker(
        IAgentHubClient hubClient,
        ICommunicationService communication,
        IOfflineCacheService cache,
        IAgentStateService state,
        IOptions<ServerConnectionOptions> options,
        ILogger<CommunicationWorker> logger,
        ICommandService? commandService = null,
        IConfigurationService? configService = null,
        IAgentHealthService? healthService = null)
    {
        _hubClient = hubClient;
        _communication = communication;
        _cache = cache;
        _state = state;
        _commandService = commandService;
        _configService = configService;
        _healthService = healthService;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CommunicationWorker started");

        await Task.Delay(2000, stoppingToken);

        await _cache.InitializeAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _hubClient.ConnectAsync(stoppingToken);
                _logger.LogInformation("Hub connected successfully");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to hub, retrying in {Delay}s", 5);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _hubClient.CommandReceived += async (s, e) =>
        {
            if (_commandService is null) return;
            try
            {
                var result = await _commandService.ExecuteCommandAsync(e.Command, stoppingToken);
                _logger.LogInformation("Command {Id} executed: {Success}", e.Command.CommandId, result.Success);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute command {Id}", e.Command.CommandId);
            }
        };

        _hubClient.ConfigUpdated += async (s, e) =>
        {
            if (_configService is null) return;
            try
            {
                var config = System.Text.Json.JsonSerializer.Deserialize<ConfigurationData>(e.ConfigJson);
                if (config != null)
                {
                    await _configService.ApplyConfigurationAsync(config);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply configuration update");
            }
        };

        _hubClient.AgentUpdateRequested += async (s, e) =>
        {
            try
            {
                _logger.LogInformation("Agent update requested: {UpdateInfo}", e.UpdateJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process update request");
            }
        };

        var syncTimer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        while (await syncTimer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SyncPendingDataAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in communication sync loop");
            }
        }
    }

    private async Task SyncPendingDataAsync(CancellationToken ct)
    {
        try
        {
            if (!_communication.IsOnline)
            {
                _logger.LogInformation("Hub offline — attempting reconnect");
                try
                {
                    await _hubClient.ConnectAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Reconnect attempt failed");
                }
                return;
            }

            var pendingEvents = await _cache.GetPendingEventsAsync();
            if (pendingEvents.Count > 0)
            {
                foreach (var evt in pendingEvents)
                {
                    try
                    {
                        var entry = System.Text.Json.JsonSerializer.Deserialize<TimelineEntryData>(evt.Payload);
                        if (entry != null)
                        {
                            await _communication.SendTimelineBatchAsync(new List<TimelineEntryData> { entry }, ct);
                        }
                    }
                    catch { }
                }

                await _cache.MarkEventsAsSentAsync(pendingEvents.Select(e => e.Id));
                _logger.LogInformation("Synced {Count} pending events", pendingEvents.Count);
            }

            var pendingScreenshots = await _cache.GetPendingScreenshotsAsync();
            if (pendingScreenshots.Count > 0)
            {
                foreach (var ss in pendingScreenshots)
                {
                    try
                    {
                        var data = Convert.FromBase64String(ss.Payload);
                        await _communication.SendScreenCaptureAsync(new ScreenCaptureData { ComputerId = _state.ComputerId, ImageData = data }, ct);
                    }
                    catch { }
                }

                await _cache.MarkScreenshotsAsSentAsync(pendingScreenshots.Select(s => s.Id));
                _logger.LogInformation("Synced {Count} pending screenshots", pendingScreenshots.Count);
            }

            await _cache.SetLastSyncAsync(DateTime.UtcNow);
            _state.LastSyncTimestamp = DateTime.UtcNow;
            _state.OfflineQueueSize = await _cache.GetQueueCountAsync();
            _state.ConnectionStatus = "Connected";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync pending data");
        }
    }
}
