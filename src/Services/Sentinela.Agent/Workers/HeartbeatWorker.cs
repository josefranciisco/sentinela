using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using Sentinela.Agent.Configuration;
using Sentinela.Agent.Recording;
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
    private readonly IRecordingStore _recordingStore;

    public HeartbeatWorker(
        IAgentStateService state,
        ICommunicationService communication,
        IOptions<AgentOptions> options,
        IScreenCaptureService screenCaptureService,
        IRecordingStore recordingStore,
        ILogger<HeartbeatWorker> logger)
    {
        _state = state;
        _communication = communication;
        _options = options.Value;
        _screenCaptureService = screenCaptureService;
        _recordingStore = recordingStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HeartbeatWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_communication.IsOnline)
            {
                try { await Task.Delay(500, stoppingToken); }
                catch (OperationCanceledException) { break; }
                continue;
            }

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

                try
                {
                    var rec = _recordingStore.GetStatus();
                    heartbeat.RecordingEnabled = _options.EnableContinuousRecording;
                    heartbeat.RecordingFromUtc = rec.FromUtc;
                    heartbeat.RecordingToUtc = rec.ToUtc;
                    heartbeat.RecordingBytes = rec.Bytes;
                    heartbeat.RecordingInSchedule = rec.InSchedule;
                    heartbeat.RecordingScheduleSummary = rec.ScheduleSummary ?? _options.RecordingSchedule.Summary();
                    heartbeat.RecordingMaxBytes = rec.MaxBytes;
                }
                catch
                {
                    heartbeat.RecordingEnabled = _options.EnableContinuousRecording;
                    heartbeat.RecordingInSchedule = _options.RecordingSchedule.IsActiveNow();
                    heartbeat.RecordingScheduleSummary = _options.RecordingSchedule.Summary();
                    heartbeat.RecordingMaxBytes = (long)(Math.Max(1, _options.RecordingMaxBytesGb) * 1024L * 1024L * 1024L);
                }
                
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
