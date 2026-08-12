using System.IO.Compression;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sentinela.Agent.Configuration;

namespace Sentinela.Agent.Services;

public interface ICommunicationService
{
    Task SendHeartbeatAsync(HeartbeatData heartbeat, CancellationToken ct = default);
    Task SendTimelineBatchAsync(List<TimelineEntryData> entries, CancellationToken ct = default);
    Task SendSecurityStatusAsync(SecurityStatusData status, CancellationToken ct = default);
    Task SendSoftwareInventoryAsync(SoftwareInventoryData inventory, CancellationToken ct = default);
    Task SendScreenCaptureAsync(ScreenCaptureData data, CancellationToken ct = default);
    Task<List<CommandData>> FetchPendingCommandsAsync(CancellationToken ct = default);
    Task<ConfigurationData> FetchConfigurationAsync(CancellationToken ct = default);
    bool IsOnline { get; }
}

public class CommunicationService : ICommunicationService
{
    private readonly IAgentHubClient _hubClient;
    private readonly IOfflineCacheService _cache;
    private readonly IAgentStateService _state;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ServerConnectionOptions _options;
    private readonly ILogger<CommunicationService> _logger;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public bool IsOnline => _hubClient.IsConnected;

    public CommunicationService(
        IAgentHubClient hubClient,
        IOfflineCacheService cache,
        IAgentStateService state,
        IHttpClientFactory httpClientFactory,
        IOptions<ServerConnectionOptions> options,
        ILogger<CommunicationService> logger)
    {
        _hubClient = hubClient;
        _cache = cache;
        _state = state;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendHeartbeatAsync(HeartbeatData heartbeat, CancellationToken ct = default)
    {
        try
        {
            if (IsOnline)
            {
                await _hubClient.SendHeartbeatAsync(heartbeat, ct);
                _state.LastSuccessfulCommunication = DateTime.UtcNow;
                _state.ConnectionStatus = "Connected";
            }
            else
            {
                await FallbackToRestAsync("heartbeat", heartbeat, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send heartbeat, queuing offline");
            await QueueOfflineAsync("heartbeat", heartbeat);
            _state.ConnectionStatus = "Disconnected";
        }
    }

    public async Task SendTimelineBatchAsync(List<TimelineEntryData> entries, CancellationToken ct = default)
    {
        if (entries.Count == 0) return;

        try
        {
            var compressed = CompressPayload(entries);
            if (IsOnline)
            {
                await SendViaSignalRAsync("timeline", compressed, async () =>
                {
                    await _hubClient.SendTimelineBatchAsync(entries, ct);
                });
            }
            else
            {
                await SendViaRestAsync("timeline/batch", compressed, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send timeline batch, queuing offline");
            await QueueOfflineAsync("timeline_batch", entries);
        }
    }

    public async Task SendSecurityStatusAsync(SecurityStatusData status, CancellationToken ct = default)
    {
        try
        {
            if (IsOnline)
            {
                await _hubClient.SendSecurityStatusAsync(status, ct);
                _state.LastSuccessfulCommunication = DateTime.UtcNow;
            }
            else
            {
                await FallbackToRestAsync("security/status", status, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send security status");
            await QueueOfflineAsync("security_status", status);
        }
    }

    public async Task SendSoftwareInventoryAsync(SoftwareInventoryData inventory, CancellationToken ct = default)
    {
        try
        {
            if (IsOnline)
            {
                await _hubClient.SendSoftwareInventoryAsync(inventory, ct);
                _state.LastSuccessfulCommunication = DateTime.UtcNow;
            }
            else
            {
                await FallbackToRestAsync("software/inventory", inventory, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send software inventory");
            await QueueOfflineAsync("software_inventory", inventory);
        }
    }

    public async Task SendScreenCaptureAsync(ScreenCaptureData data, CancellationToken ct = default)
    {
        try
        {
            if (IsOnline)
            {
                await _hubClient.SendScreenCaptureAsync(data, ct);
            }
            else
            {
                await FallbackToRestAsync("screencapture", data.ImageData, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send screen capture");
            var b64 = Convert.ToBase64String(data.ImageData);
            await QueueOfflineAsync("screenshot", b64);
        }
    }

    public async Task<List<CommandData>> FetchPendingCommandsAsync(CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("SentinelaApi");
            var response = await client.GetAsync($"/api/commands/pending?computerId={_state.ComputerId}", ct);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<List<CommandData>>(cancellationToken: ct);
                return result ?? new List<CommandData>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch pending commands");
        }
        return new List<CommandData>();
    }

    public async Task<ConfigurationData> FetchConfigurationAsync(CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("SentinelaApi");
            var response = await client.GetAsync($"/api/agent/configuration?computerId={_state.ComputerId}", ct);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ConfigurationData>(cancellationToken: ct)
                    ?? new ConfigurationData();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch configuration");
        }
        return new ConfigurationData();
    }

    private async Task FallbackToRestAsync<T>(string endpoint, T payload, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("SentinelaApi");
            var json = JsonSerializer.Serialize(payload);
            var compressed = CompressString(json);
            var content = new ByteArrayContent(compressed);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            content.Headers.ContentEncoding.Add("gzip");

            var response = await client.PostAsync($"/api/agent/{endpoint}", content, ct);
            if (response.IsSuccessStatusCode)
            {
                _state.LastSuccessfulCommunication = DateTime.UtcNow;
                _state.ConnectionStatus = "Connected";
            }
        }
        catch
        {
            throw;
        }
    }

    private async Task SendViaSignalRAsync<T>(string type, T payload, Func<Task> sendAction)
    {
        await _sendLock.WaitAsync();
        try
        {
            await sendAction();
            _state.LastSuccessfulCommunication = DateTime.UtcNow;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task SendViaRestAsync<T>(string endpoint, T payload, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("SentinelaApi");
        var response = await client.PostAsJsonAsync($"/api/agent/{endpoint}", payload, ct);
        if (response.IsSuccessStatusCode)
        {
            _state.LastSuccessfulCommunication = DateTime.UtcNow;
        }
    }

    private async Task QueueOfflineAsync<T>(string eventType, T payload)
    {
        var json = JsonSerializer.Serialize(payload);
        await _cache.QueueEventAsync(eventType, json);
        _state.OfflineQueueSize = await _cache.GetQueueCountAsync();
    }

    private byte[] CompressPayload<T>(T payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return CompressString(json);
    }

    private byte[] CompressString(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        using var ms = new MemoryStream();
        using (var gzip = new GZipStream(ms, CompressionLevel.Fastest))
        {
            gzip.Write(bytes, 0, bytes.Length);
        }
        return ms.ToArray();
    }
}

public class HeartbeatData
{
    public string ComputerId { get; set; } = "";
    public string Hostname { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string CurrentUser { get; set; } = "";
    public string Status { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public int Uptime { get; set; }
    public string AgentVersion { get; set; } = "";
    public bool IsAgentUpdated { get; set; }
    public int MonitorCount { get; set; } = 1;
    public Guid? TenantId { get; set; }
}

public class TimelineEntryData
{
    public string ComputerId { get; set; } = "";
    public string EventType { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Username { get; set; }
    public string? Details { get; set; }
    public string Severity { get; set; } = "Info";
    public DateTime Timestamp { get; set; }
}

public class SecurityStatusData
{
    public string ComputerId { get; set; } = "";
    public bool FirewallEnabled { get; set; }
    public bool DefenderEnabled { get; set; }
    public bool AntivirusEnabled { get; set; }
    public bool RealTimeProtectionEnabled { get; set; }
    public int AntivirusSignatureAgeDays { get; set; }
    public DateTime? AntivirusSignatureLastUpdated { get; set; }
    public string AntivirusProductName { get; set; } = "";
    public bool BitlockerEnabled { get; set; }
    public bool RdpEnabled { get; set; }
    public string Hostname { get; set; } = "";
    public DateTime Timestamp { get; set; }
}

public class ScreenCaptureData
{
    public string ComputerId { get; set; } = "";
    public byte[] ImageData { get; set; } = Array.Empty<byte>();
    public string? CaptureRequestId { get; set; }
}

public class SoftwareInventoryData
{
    public string ComputerId { get; set; } = "";
    public string Hostname { get; set; } = "";
    public List<SoftwareInventoryItem> Items { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

public class SoftwareInventoryItem
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Publisher { get; set; } = "";
    public DateTime? InstallDate { get; set; }
    public string InstallLocation { get; set; } = "";
}

public class CommandData
{
    public string CommandId { get; set; } = "";
    public string CommandType { get; set; } = "";
    public string Parameters { get; set; } = "";
    public DateTime ReceivedAt { get; set; }
}

public class CommandReceivedEventArgs : EventArgs
{
    public CommandData Command { get; }
    public CommandReceivedEventArgs(CommandData command) => Command = command;
}

public class ConfigUpdateEventArgs : EventArgs
{
    public string ConfigJson { get; }
    public ConfigUpdateEventArgs(string configJson) => ConfigJson = configJson;
}

public class AgentUpdateEventArgs : EventArgs
{
    public string UpdateJson { get; }
    public AgentUpdateEventArgs(string updateJson) => UpdateJson = updateJson;
}

public class RemoteSessionRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string SessionType { get; set; } = "view";
    public int? MonitorIndex { get; set; }
}

public class RemoteSessionStartedEventArgs : EventArgs
{
    public string SessionId { get; }
    public string SessionType { get; }
    public int? MonitorIndex { get; }
    public RemoteSessionStartedEventArgs(string sessionId, string sessionType, int? monitorIndex = null)
    {
        SessionId = sessionId;
        SessionType = sessionType;
        MonitorIndex = monitorIndex;
    }
}

public class RemoteSessionStoppedEventArgs : EventArgs
{
    public string SessionId { get; }
    public RemoteSessionStoppedEventArgs(string sessionId) => SessionId = sessionId;
}

public class RemoteSessionMonitorChangedEventArgs : EventArgs
{
    public string SessionId { get; }
    public int? MonitorIndex { get; }
    public RemoteSessionMonitorChangedEventArgs(string sessionId, int? monitorIndex)
    {
        SessionId = sessionId;
        MonitorIndex = monitorIndex;
    }
}

public class RemoteScreenFrameData
{
    public string SessionId { get; set; } = string.Empty;
    public byte[] FrameData { get; set; } = Array.Empty<byte>();
    public long FrameNumber { get; set; }
    public DateTime Timestamp { get; set; }
}

public class ConfigurationData
{
    public int HeartbeatIntervalMs { get; set; } = 10000;
    public int CollectorIntervalMs { get; set; } = 1000;
    public int BatchSendIntervalMs { get; set; } = 5000;
    public bool EnableScreenCapture { get; set; }
    public int ScreenCaptureQuality { get; set; } = 50;
    public int ScreenCaptureIntervalMs { get; set; } = 300000;
    public bool EnableUsbTracking { get; set; } = true;
    public string[] MonitoredProcessNames { get; set; } = Array.Empty<string>();
}
