namespace Sentinela.RemoteAssistance.Core;

public class RemoteSession
{
    public Guid Id { get; set; }
    public Guid ComputerId { get; set; }
    public string ComputerName { get; set; } = string.Empty;
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public Guid? EndUserId { get; set; }
    public string EndUserName { get; set; } = string.Empty;
    public SessionMode Mode { get; set; } = SessionMode.ViewOnly;
    public SessionStatus Status { get; set; } = SessionStatus.Requested;
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string? Justification { get; set; }
    public bool IsAudited { get; set; } = true;
    public string ConnectionId { get; set; } = string.Empty;
    public string RemoteIpAddress { get; set; } = string.Empty;
    public List<SessionActivity> Activities { get; set; } = new();
}

public enum SessionMode
{
    ViewOnly,
    FullControl,
    Chat,
    FileTransfer
}

public enum SessionStatus
{
    Requested,
    Connecting,
    Connected,
    Disconnecting,
    Disconnected,
    Failed,
    TimedOut,
    Rejected
}

public class SessionActivity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public ActivityType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string? Details { get; set; }
}

public enum ActivityType
{
    SessionStarted,
    SessionEnded,
    ScreenViewStarted,
    ScreenViewEnded,
    ControlRequested,
    ControlGranted,
    ControlDenied,
    ChatMessage,
    FileTransferStarted,
    FileTransferCompleted,
    CommandExecuted,
    KeyStroke,
    MouseClick,
    Error
}
