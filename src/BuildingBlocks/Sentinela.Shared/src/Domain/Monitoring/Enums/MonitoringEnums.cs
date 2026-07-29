namespace Sentinela.Shared.Domain.Monitoring.Enums;

public enum EventType
{
    Login,
    Logout,
    Lock,
    Unlock,
    AppStarted,
    AppClosed,
    AppFocus,
    USBConnected,
    USBDisconnected,
    SoftwareInstalled,
    SoftwareUninstalled,
    FileCopy,
    Print,
    Error,
    IdleStart,
    IdleEnd,
    MalwareDetected,
    AntivirusOutdated,
    AntivirusDisabled,
    Custom
}

public enum ComputerStatus
{
    Online,
    Offline,
    Away,
    Disabled
}

public enum Severity
{
    Info,
    Low,
    Medium,
    High,
    Critical
}
