namespace Sentinela.RemoteAssistance.Core;

public class RemoteCommand
{
    public Guid Id { get; set; }
    public Guid ComputerId { get; set; }
    public CommandType Type { get; set; }
    public string? Parameters { get; set; }
    public string IssuedBy { get; set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;
    public CommandStatus Status { get; set; } = CommandStatus.Pending;
    public string? Result { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public enum CommandType
{
    Restart,
    Shutdown,
    Logoff,
    Lock,
    SendMessage,
    UpdateAgent,
    ExecutePowerShell,
    ExecuteCMD,
    ExecuteScript,
    TransferFile,
    OpenApplication,
    KillProcess,
    StartService,
    StopService,
    GetSystemInfo,
    GetProcessList,
    GetServices,
    RegistryRead,
    RegistryWrite
}

public enum CommandStatus
{
    Pending,
    Sent,
    Received,
    Executing,
    Completed,
    Failed,
    TimedOut
}
