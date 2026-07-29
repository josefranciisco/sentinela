using Sentinela.Shared.Core.Entities;
using Sentinela.Shared.Domain.Monitoring.Enums;

namespace Sentinela.Shared.Domain.Security;

public class SecurityEvent : AggregateRoot
{
    private readonly Dictionary<string, string> _metadata = new();

    protected SecurityEvent() : base() { }

    public SecurityEvent(
        Guid computerId,
        string eventType,
        string category,
        string description,
        string? username = null,
        string? sourceIp = null,
        Severity severity = Severity.Info)
        : base()
    {
        ComputerId = computerId;
        EventType = eventType;
        Category = category;
        Description = description;
        Username = username;
        SourceIp = sourceIp;
        Timestamp = DateTimeOffset.UtcNow;
        Severity = severity;
        IsAcknowledged = false;
        IsResolved = false;
    }

    public Guid ComputerId { get; private set; }
    public string EventType { get; private set; }
    public string Category { get; private set; }
    public string Description { get; private set; }
    public string? Username { get; private set; }
    public string? SourceIp { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
    public Severity Severity { get; private set; }
    public bool IsAcknowledged { get; private set; }
    public bool IsResolved { get; private set; }

    public IReadOnlyDictionary<string, string> Metadata => _metadata;

    public void AddMetadata(string key, string value)
    {
        _metadata[key] = value;
    }

    public void Acknowledge()
    {
        IsAcknowledged = true;
    }

    public void Resolve()
    {
        IsResolved = true;
    }
}
