using Sentinela.Shared.Core.Entities;
using Sentinela.Shared.Domain.Monitoring.Enums;

namespace Sentinela.Shared.Domain.Security;

public class SecurityAlert : AggregateRoot
{
    private readonly List<string> _tags = new();

    protected SecurityAlert() : base() { }

    public SecurityAlert(
        string title,
        string description,
        Severity severity,
        string category,
        string? source = null,
        Guid? computerId = null,
        string? username = null)
        : base()
    {
        Title = title;
        Description = description;
        Severity = severity;
        Category = category;
        Source = source;
        ComputerId = computerId;
        Username = username;
        Status = AlertStatus.Open;
        Timestamp = DateTimeOffset.UtcNow;
    }

    public string Title { get; private set; }
    public string Description { get; private set; }
    public Severity Severity { get; private set; }
    public string Category { get; private set; }
    public string? Source { get; private set; }
    public Guid? ComputerId { get; private set; }
    public string? Username { get; private set; }
    public AlertStatus Status { get; private set; }
    public string? AssignedTo { get; private set; }
    public double CorrelationScore { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
    public string? Resolution { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public string? ResolvedBy { get; private set; }

    public IReadOnlyList<string> Tags => _tags.AsReadOnly();

    public void Assign(string assignedTo)
    {
        AssignedTo = assignedTo;
        Status = AlertStatus.InProgress;
    }

    public void Resolve(string resolution, string resolvedBy)
    {
        Status = AlertStatus.Resolved;
        Resolution = resolution;
        ResolvedAt = DateTimeOffset.UtcNow;
        ResolvedBy = resolvedBy;
    }

    public void MarkAsFalsePositive()
    {
        Status = AlertStatus.FalsePositive;
    }

    public void Acknowledge()
    {
        if (Status == AlertStatus.Open)
            Status = AlertStatus.Acknowledged;
    }

}
