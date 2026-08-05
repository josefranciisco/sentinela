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

// ScreenCapture and CaptureStatus moved to Sentinela.Persistence.Models.ScreenCapture

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

// RemoteSession moved to Sentinela.Persistence.Models.RemoteSession

// SessionStatus moved to Sentinela.Persistence.Models (string-based)

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
