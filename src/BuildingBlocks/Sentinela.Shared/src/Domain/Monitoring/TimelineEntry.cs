using Sentinela.Shared.Core.Entities;
using Sentinela.Shared.Domain.Monitoring.Enums;

namespace Sentinela.Shared.Domain.Monitoring;

public class TimelineEntry : BaseEntity
{
    protected TimelineEntry() : base() { }

    public TimelineEntry(
        DateTimeOffset timestamp,
        EventType eventType,
        string category,
        string description,
        Guid computerId,
        string? username = null,
        string? details = null,
        Severity severity = Severity.Info)
        : base()
    {
        Timestamp = timestamp;
        EventType = eventType;
        Category = category;
        Description = description;
        ComputerId = computerId;
        Username = username;
        Details = details;
        Severity = severity;
    }

    public DateTimeOffset Timestamp { get; private set; }
    public EventType EventType { get; private set; }
    public string Category { get; private set; }
    public string Description { get; private set; }
    public Guid ComputerId { get; private set; }
    public string? Username { get; private set; }
    public string? Details { get; private set; }
    public Severity Severity { get; private set; }
}
