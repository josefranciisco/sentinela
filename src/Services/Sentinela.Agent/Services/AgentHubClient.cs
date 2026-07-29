using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using Sentinela.Agent.Configuration;

namespace Sentinela.Agent.Services;

public interface IAgentHubClient
{
    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync();
    Task SendHeartbeatAsync(HeartbeatData heartbeat, CancellationToken ct = default);
    Task SendTimelineBatchAsync(List<TimelineEntryData> entries, CancellationToken ct = default);
    Task SendSecurityStatusAsync(SecurityStatusData status, CancellationToken ct = default);
    Task SendSoftwareInventoryAsync(SoftwareInventoryData inventory, CancellationToken ct = default);
    Task SendScreenCaptureAsync(ScreenCaptureData data, CancellationToken ct = default);
    bool IsConnected { get; }
    event EventHandler<CommandReceivedEventArgs>? CommandReceived;
    event EventHandler<ConfigUpdateEventArgs>? ConfigUpdated;
    event EventHandler<AgentUpdateEventArgs>? AgentUpdateRequested;
}

public class AgentHubClient : IAgentHubClient, IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly ServerConnectionOptions _options;
    private readonly ILogger<AgentHubClient> _logger;
    private readonly CancellationTokenSource _reconnectCts = new();
    private bool _disposed;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public event EventHandler<CommandReceivedEventArgs>? CommandReceived;
    public event EventHandler<ConfigUpdateEventArgs>? ConfigUpdated;
    public event EventHandler<AgentUpdateEventArgs>? AgentUpdateRequested;

    public AgentHubClient(IOptions<ServerConnectionOptions> options, ILogger<AgentHubClient> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_connection != null) await DisconnectAsync();

        var builder = new HubConnectionBuilder()
            .WithUrl(_options.SignalRUrl, opts =>
            {
                if (!string.IsNullOrEmpty(_options.ApiKey))
                    opts.Headers["X-Api-Key"] = _options.ApiKey;
            })
            .WithAutomaticReconnect(new RetryPolicy())
            .ConfigureLogging(logging => logging.AddSerilog());

        _connection = builder.Build();

        _connection.On<HeartbeatData>("SendHeartbeat", async (hb) =>
        {
            _logger.LogInformation("Received heartbeat ack");
            await Task.CompletedTask;
        });

        _connection.On<List<TimelineEntryData>>("SendTimelineBatch", async (entries) =>
        {
            _logger.LogInformation("Received timeline batch ack for {Count} entries", entries.Count);
            await Task.CompletedTask;
        });

        _connection.On<SecurityStatusData>("SendSecurityStatus", async (status) =>
        {
            _logger.LogInformation("Received security status ack");
            await Task.CompletedTask;
        });

        _connection.On<string>("ExecuteCommand", async (commandJson) =>
        {
            var cmd = System.Text.Json.JsonSerializer.Deserialize<CommandData>(commandJson);
            if (cmd != null)
                CommandReceived?.Invoke(this, new CommandReceivedEventArgs(cmd));
            await Task.CompletedTask;
        });

        _connection.On<string>("UpdateConfig", async (configJson) =>
        {
            ConfigUpdated?.Invoke(this, new ConfigUpdateEventArgs(configJson));
            await Task.CompletedTask;
        });

        _connection.On<string>("UpdateAgent", async (updateJson) =>
        {
            AgentUpdateRequested?.Invoke(this, new AgentUpdateEventArgs(updateJson));
            await Task.CompletedTask;
        });

        _connection.Closed += async (error) =>
        {
            _logger.LogWarning(error, "Connection closed");
            await Task.Delay(_options.ReconnectDelayMs, _reconnectCts.Token);
            if (!_disposed) await ConnectAsync(ct);
        };

        await _connection.StartAsync(ct);
        _logger.LogInformation("Connected to SignalR hub at {Url}", _options.SignalRUrl);
    }

    public async Task DisconnectAsync()
    {
        if (_connection != null)
        {
            await _connection.StopAsync();
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    public async Task SendHeartbeatAsync(HeartbeatData heartbeat, CancellationToken ct = default)
    {
        if (!IsConnected) return;
        try
        {
            await _connection!.InvokeAsync("SendHeartbeat", heartbeat, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send heartbeat");
        }
    }

    public async Task SendTimelineBatchAsync(List<TimelineEntryData> entries, CancellationToken ct = default)
    {
        if (!IsConnected || entries.Count == 0) return;
        try
        {
            await _connection!.InvokeAsync("SendTimelineBatch", entries, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send timeline batch");
        }
    }

    public async Task SendSecurityStatusAsync(SecurityStatusData status, CancellationToken ct = default)
    {
        if (!IsConnected) return;
        try
        {
            await _connection!.InvokeAsync("SendSecurityStatus", status, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send security status");
        }
    }

    public async Task SendSoftwareInventoryAsync(SoftwareInventoryData inventory, CancellationToken ct = default)
    {
        if (!IsConnected) return;
        try
        {
            await _connection!.InvokeAsync("SendSoftwareInventory", inventory, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send software inventory");
        }
    }

    public async Task SendScreenCaptureAsync(ScreenCaptureData data, CancellationToken ct = default)
    {
        if (!IsConnected) return;
        try
        {
            await _connection!.InvokeAsync("SendScreenCapture", data, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send screen capture");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _reconnectCts.Cancel();
        await DisconnectAsync();
        _reconnectCts.Dispose();
    }

    private class RetryPolicy : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            if (retryContext.PreviousRetryCount >= 5) return null;
            var delay = TimeSpan.FromSeconds(Math.Pow(2, retryContext.PreviousRetryCount));
            return delay > TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(30) : delay;
        }
    }
}
