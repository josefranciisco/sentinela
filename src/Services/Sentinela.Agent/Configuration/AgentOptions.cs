namespace Sentinela.Agent.Configuration;

public class AgentOptions
{
    public Guid? TenantId { get; set; }
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
    public bool EnableContinuousRecording { get; set; } = true;
    public double RecordingFps { get; set; } = 2;
    public int RecordingQuality { get; set; } = 55;
    public int RecordingMaxWidth { get; set; } = 1280;
    public int RecordingRetentionHours { get; set; } = 72;
    public int RecordingIdleSeconds { get; set; } = 15;
    public double RecordingMaxBytesGb { get; set; } = 8;
    public RecordingScheduleOptions RecordingSchedule { get; set; } = new();
}

public class RecordingScheduleOptions
{
    public bool Enabled { get; set; }
    public int StartHour { get; set; } = 7;
    public int EndHour { get; set; } = 19;
    /// <summary>0 = domingo … 6 = sábado (.NET DayOfWeek). Padrão: segunda a sexta.</summary>
    public int[] DaysOfWeek { get; set; } = [1, 2, 3, 4, 5];

    public bool IsActiveNow(DateTime? localNow = null)
    {
        if (!Enabled) return true;
        var now = localNow ?? DateTime.Now;
        var days = DaysOfWeek is { Length: > 0 } ? DaysOfWeek : [1, 2, 3, 4, 5];
        if (!days.Contains((int)now.DayOfWeek)) return false;
        var start = now.Date.AddHours(Math.Clamp(StartHour, 0, 23));
        var end = now.Date.AddHours(Math.Clamp(EndHour, 0, 24));
        if (end <= start)
            return now >= start || now < end;
        return now >= start && now < end;
    }

    public string Summary()
    {
        if (!Enabled) return "24h";
        return $"seg–sex, {Math.Clamp(StartHour, 0, 23):00}:00–{Math.Clamp(EndHour, 0, 24):00}:00";
    }
}

public class ServerConnectionOptions
{
    public string ApiUrl { get; set; } = "http://localhost:5002";
    public string SignalRUrl { get; set; } = "http://localhost:5002/hubs/agent";
    public string ApiKey { get; set; } = string.Empty;
    public string ClientCertificatePath { get; set; } = string.Empty;
    public int ReconnectDelayMs { get; set; } = 1000;
    public int MaxReconnectDelayMs { get; set; } = 30000;
    public bool UseCertificate { get; set; } = false;
}
