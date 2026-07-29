using Microsoft.Extensions.Options;
using Sentinela.Agent.Configuration;
using Sentinela.Agent.Core.Health;
using Sentinela.Agent.Services;

namespace Sentinela.Agent.Workers;

public class WatchdogWorker : BackgroundService
{
    private readonly IWatchdogService _watchdog;
    private readonly IAgentHealthService _healthService;
    private readonly IAgentStateService _state;
    private readonly AgentOptions _options;
    private readonly ILogger<WatchdogWorker> _logger;

    public WatchdogWorker(
        IWatchdogService watchdog,
        IAgentHealthService healthService,
        IAgentStateService state,
        IOptions<AgentOptions> options,
        ILogger<WatchdogWorker> logger)
    {
        _watchdog = watchdog;
        _healthService = healthService;
        _state = state;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WatchdogWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformHealthCheckAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in watchdog health check");
            }

            await Task.Delay(_options.HealthCheckIntervalMs, stoppingToken);
        }
    }

    private async Task PerformHealthCheckAsync()
    {
        var isHealthy = _watchdog.IsAgentHealthy();

        if (!isHealthy)
        {
            _logger.LogWarning("Agent health check failed");

            var issues = new List<string>();

            var health = _healthService.GetCurrentHealth();
            if (health.MemoryUsageMb > 500)
                issues.Add($"High memory: {health.MemoryUsageMb}MB");

            if (health.QueueSize > _options.OfflineQueueMaxSize)
                issues.Add($"Queue overflow: {health.QueueSize} items");

            if (_state.ConnectionStatus == "Disconnected")
                issues.Add("Connection lost");

            if (issues.Count > 0)
            {
                _logger.LogWarning("Watchdog issues: {Issues}", string.Join("; ", issues));
            }

            var selfDiagnosis = _healthService.SelfDiagnose();
            if (!selfDiagnosis)
            {
                _logger.LogWarning("Self-diagnosis failed, reporting");
                _watchdog.ReportCrash("SelfDiagnosis");
            }

            var collectorHealth = _watchdog.CheckCollectorsHealth();
            foreach (var (collector, running) in collectorHealth)
            {
                if (!running)
                {
                    _logger.LogWarning("Collector {Collector} is not responding", collector);
                    _watchdog.ReportCrash(collector);
                }
            }
        }
        else
        {
            _healthService.SelfDiagnose();
        }
    }
}
