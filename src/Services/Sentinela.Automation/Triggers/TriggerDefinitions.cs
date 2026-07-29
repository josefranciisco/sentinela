namespace Sentinela.Automation.Triggers;

public enum TriggerType
{
    USBConnected,
    USBDisconnected,
    SoftwareInstalled,
    SoftwareUninstalled,
    SecurityEvent,
    FailedLogin,
    LoginOutOfHours,
    NewAdminUser,
    ServiceStopped,
    FirewallDisabled,
    DefenderDisabled,
    HighSeverityAlert,
    CriticalAlert,
    ApplicationStarted,
    ApplicationStopped,
    ScreenCapture,
    Schedule,
    Custom
}

public class TriggerDefinition
{
    public TriggerType Type { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public Dictionary<string, string> Parameters { get; set; } = new();
}
