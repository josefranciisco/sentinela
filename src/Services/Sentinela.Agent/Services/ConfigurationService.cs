using System.Text.Json;
using Microsoft.Extensions.Options;
using Sentinela.Agent.Configuration;

namespace Sentinela.Agent.Services;

public interface IConfigurationService
{
    Task<ConfigurationData> LoadConfigurationAsync();
    Task ApplyConfigurationAsync(ConfigurationData config);
    ConfigurationData GetCurrentConfiguration();
    event EventHandler<ConfigurationData>? ConfigurationApplied;
}

public class ConfigurationService : IConfigurationService
{
    private readonly ICommunicationService _communication;
    private readonly IOptions<AgentOptions> _localOptions;
    private readonly IOptions<ServerConnectionOptions> _serverOptions;
    private readonly ILogger<ConfigurationService> _logger;
    private ConfigurationData _currentConfig;
    private readonly object _lock = new();

    public event EventHandler<ConfigurationData>? ConfigurationApplied;

    public ConfigurationService(
        ICommunicationService communication,
        IOptions<AgentOptions> localOptions,
        IOptions<ServerConnectionOptions> serverOptions,
        ILogger<ConfigurationService> logger)
    {
        _communication = communication;
        _localOptions = localOptions;
        _serverOptions = serverOptions;
        _logger = logger;

        _currentConfig = new ConfigurationData
        {
            HeartbeatIntervalMs = localOptions.Value.HeartbeatIntervalMs,
            CollectorIntervalMs = localOptions.Value.CollectorIntervalMs,
            BatchSendIntervalMs = localOptions.Value.BatchSendIntervalMs,
            EnableScreenCapture = localOptions.Value.EnableScreenCapture,
            ScreenCaptureQuality = localOptions.Value.ScreenCaptureQuality,
            ScreenCaptureIntervalMs = localOptions.Value.ScreenCaptureIntervalMs,
            EnableUsbTracking = localOptions.Value.EnableUsbTracking,
            MonitoredProcessNames = localOptions.Value.MonitoredProcessNames
        };
    }

    public async Task<ConfigurationData> LoadConfigurationAsync()
    {
        try
        {
            if (_communication.IsOnline)
            {
                var serverConfig = await _communication.FetchConfigurationAsync();
                if (serverConfig != null)
                {
                    await ApplyConfigurationAsync(serverConfig);
                    return serverConfig;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load server configuration, using local fallback");
        }
        return _currentConfig;
    }

    public Task ApplyConfigurationAsync(ConfigurationData config)
    {
        lock (_lock)
        {
            _currentConfig = config;
        }

        _logger.LogInformation("Configuration applied: {Config}", JsonSerializer.Serialize(config));
        ConfigurationApplied?.Invoke(this, config);
        return Task.CompletedTask;
    }

    public ConfigurationData GetCurrentConfiguration()
    {
        lock (_lock)
        {
            return new ConfigurationData
            {
                HeartbeatIntervalMs = _currentConfig.HeartbeatIntervalMs,
                CollectorIntervalMs = _currentConfig.CollectorIntervalMs,
                BatchSendIntervalMs = _currentConfig.BatchSendIntervalMs,
                EnableScreenCapture = _currentConfig.EnableScreenCapture,
                ScreenCaptureQuality = _currentConfig.ScreenCaptureQuality,
                ScreenCaptureIntervalMs = _currentConfig.ScreenCaptureIntervalMs,
                EnableUsbTracking = _currentConfig.EnableUsbTracking,
                MonitoredProcessNames = (string[])_currentConfig.MonitoredProcessNames.Clone()
            };
        }
    }
}
