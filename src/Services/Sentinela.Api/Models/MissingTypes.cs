using Sentinela.Shared.Core.Entities;
using Sentinela.Shared.Messaging.Events;

namespace Sentinela.Api.Models;

public class AgentCommandEvent : IEvent
{
    public AgentCommandEvent(Guid computerId, string commandType, string payload, string? issuedBy)
    {
        Id = Guid.NewGuid();
        Timestamp = DateTimeOffset.UtcNow;
        EventType = "AgentCommand";
        Source = "Sentinela.Api";
        ComputerId = computerId;
        CommandType = commandType;
        Payload = payload;
        IssuedBy = issuedBy;
    }

    public Guid Id { get; }
    public DateTimeOffset Timestamp { get; }
    public string EventType { get; }
    public string Source { get; }
    public Guid ComputerId { get; }
    public string CommandType { get; }
    public string Payload { get; }
    public string? IssuedBy { get; }
}

public enum CaptureStatus
{
    Pending,
    Captured,
    Failed,
    Expired
}

public enum SessionStatus
{
    Pending,
    Active,
    Terminated,
    Expired
}

public class ScreenCapture : BaseEntity
{
    public Guid ComputerId { get; set; }
    public byte[]? ImageData { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public CaptureStatus Status { get; set; }
    public string? RequestedBy { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
}

public class SoftwareInventory : BaseEntity
{
    public Guid ComputerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public bool IsAuthorized { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTimeOffset FirstDetected { get; set; }
    public DateTimeOffset LastDetected { get; set; }
}

// Prefer Sentinela.Persistence.Models.SoftwareInventoryItem for persistence.

public class RemoteSession : BaseEntity
{
    public Guid ComputerId { get; set; }
    public string? RequestedBy { get; set; }
    public string SessionType { get; set; } = string.Empty;
    public SessionStatus Status { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? TerminatedAt { get; set; }
    public string? TerminatedBy { get; set; }
}

public class AgentCommand
{
    public Guid ComputerId { get; set; }
    public string CommandType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string? IssuedBy { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
}

public class AgentConfiguration
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class AuditLog : BaseEntity
{
    public string? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
