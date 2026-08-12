using Microsoft.Extensions.Options;
using Sentinela.Agent.Configuration;
using Sentinela.Agent.Core.Monitors;
using Sentinela.Agent.Services;

namespace Sentinela.Agent.Workers;

public class HeartbeatWorker : BackgroundService
{
    private readonly IAgentStateService _state;
    private readonly ICommunicationService _communication;
    private readonly ILogger<HeartbeatWorker> _logger;
    private readonly AgentOptions _options;
    private readonly IScreenCaptureService _screenCaptureService;

    public HeartbeatWorker(IAgentStateService state, ICommunicationService communication, 
        IOptions<AgentOptions> options, IScreenCaptureService screenCaptureService, ILogger<HeartbeatWorker> logger)
    {
        _state = state;
        _communication = communication;
        _options = options.Value;
        _screenCaptureService = screenCaptureService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HeartbeatWorker started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var heartbeat = new HeartbeatData
                {
                    ComputerId = _state.ComputerId,
                    Hostname = Environment.MachineName,
                    Timestamp = DateTime.UtcNow,
                    CurrentUser = _state.CurrentUser,
                    Status = _state.ConnectionStatus,
                    IpAddress = await GetLocalIpAddressAsync(),
                    Uptime = (int)(DateTime.UtcNow - _state.StartTime).TotalSeconds,
                    AgentVersion = GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0",
                    IsAgentUpdated = true,
                    MonitorCount = _screenCaptureService.GetMonitors().Count,
                    TenantId = _options.TenantId
                };
                
                await _communication.SendHeartbeatAsync(heartbeat, stoppingToken);
                _state.LastHeartbeat = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send heartbeat");
                _state.ConnectionStatus = "Disconnected";
            }
            
            await Task.Delay(_options.HeartbeatIntervalMs, stoppingToken);
        }
    }
    
    private async Task<string> GetLocalIpAddressAsync()
    {
        var host = await Dns.GetHostEntryAsync(Dns.GetHostName());
        return host.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)?.ToString() ?? "127.0.0.1";
    }
}
