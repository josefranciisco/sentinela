namespace Sentinela.Api.Services;

public class ComputerPresenceWorker : BackgroundService
{
    private readonly IRepository<Computer> _computerRepo;
    private readonly IHubContext<AgentHub> _agentHub;
    private readonly IHubContext<MonitoringHub> _monitoringHub;
    private readonly ILogger<ComputerPresenceWorker> _logger;
    private readonly TimeSpan _offlineAfter;
    private readonly TimeSpan _sweepInterval;

    public ComputerPresenceWorker(
        IRepository<Computer> computerRepo,
        IHubContext<AgentHub> agentHub,
        IHubContext<MonitoringHub> monitoringHub,
        IConfiguration configuration,
        ILogger<ComputerPresenceWorker> logger)
    {
        _computerRepo = computerRepo;
        _agentHub = agentHub;
        _monitoringHub = monitoringHub;
        _logger = logger;
        _offlineAfter = TimeSpan.FromMinutes(configuration.GetValue("Presence:OfflineAfterMinutes", 5));
        _sweepInterval = TimeSpan.FromSeconds(configuration.GetValue("Presence:SweepIntervalSeconds", 30));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Computer presence worker started (offline after {OfflineAfter}, sweep every {SweepInterval})",
            _offlineAfter, _sweepInterval);

        using var timer = new PeriodicTimer(_sweepInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Presence sweep failed");
            }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        var staleThreshold = DateTimeOffset.UtcNow.Subtract(_offlineAfter);
        var staleComputers = await _computerRepo.Query()
            .Where(c => c.Status == ComputerStatus.Online && !c.IsDeleted && c.LastHeartbeat < staleThreshold)
            .ToListAsync(ct);

        if (staleComputers.Count == 0) return;

        foreach (var computer in staleComputers)
        {
            computer.MarkOffline();
            _logger.LogInformation("Computer {Hostname} ({Id}) marked offline - last heartbeat {LastHeartbeat:R}",
                computer.Hostname, computer.Id, computer.LastHeartbeat);
        }

        await _computerRepo.SaveChangesAsync(ct);

        foreach (var computer in staleComputers)
        {
            var payload = new
            {
                computer.Id,
                computer.Hostname,
                computer.Status,
                computer.LastHeartbeat
            };

            await _agentHub.Clients.Group("admins").SendAsync("ComputerStatusChanged", payload, ct);
            await _monitoringHub.Clients.Group("admins").SendAsync("ComputerStatusChanged", payload, ct);
        }
    }
}