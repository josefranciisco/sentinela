namespace Sentinela.Agent.Configuration;

public class AgentOptions
{
    public int HeartbeatIntervalMs { get; set; } = 10000;
    public int CollectorIntervalMs { get; set; } = 1000;
    public int BatchSendIntervalMs { get; set; } = 5000;
    public int OfflineQueueMaxSize { get; set; } = 10000;
    public int HealthCheckIntervalMs { get; set; } = 30000;
    public bool EnableScreenCapture { get; set; } = false;
    public int ScreenCaptureQuality { get; set; } = 50;
    public int ScreenCaptureIntervalMs { get; set; } = 300000;
    public bool EnableUsbTracking { get; set; } = true;
    public bool EnableFileTracking { get; set; } = false;
    public string[] MonitoredProcessNames { get; set; } = Array.Empty<string>();
    public string LogLevel { get; set; } = "Information";
}

public class ServerConnectionOptions
{
    public string ApiUrl { get; set; } = "https://localhost:5001";
    public string SignalRUrl { get; set; } = "https://localhost:5001/hubs/agent";
    public string ApiKey { get; set; } = string.Empty;
    public string ClientCertificatePath { get; set; } = string.Empty;
    public int ReconnectDelayMs { get; set; } = 1000;
    public int MaxReconnectDelayMs { get; set; } = 30000;
    public bool UseCertificate { get; set; } = false;
}
