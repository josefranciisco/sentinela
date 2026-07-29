using Sentinela.Shared.Core.Entities;
using Sentinela.Shared.Domain.Monitoring.Enums;
using Sentinela.Shared.Domain.Security;

namespace Sentinela.Shared.Domain.Alerting;

public class Alert : AggregateRoot
{
    private readonly List<AlertComment> _comments = new();
    private readonly Dictionary<string, string> _metadata = new();

    protected Alert() : base() { }

    public Alert(
        Guid ruleId,
        Guid? computerId,
        string title,
        string description,
        Severity severity,
        string category,
        string? source = null)
        : base()
    {
        RuleId = ruleId;
        ComputerId = computerId;
        Title = title;
        Description = description;
        Severity = severity;
        Category = category;
        Source = source;
        Status = AlertStatus.Open;
        Timestamp = DateTimeOffset.UtcNow;
    }

    public Guid RuleId { get; private set; }
    public Guid? ComputerId { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public Severity Severity { get; private set; }
    public string Category { get; private set; }
    public string? Source { get; private set; }
    public AlertStatus Status { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
    public string? AssignedTo { get; private set; }
    public DateTimeOffset? AcknowledgedAt { get; private set; }
    public string? AcknowledgedBy { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public string? ResolvedBy { get; private set; }
    public string? Resolution { get; private set; }

    public IReadOnlyList<AlertComment> Comments => _comments.AsReadOnly();
    public IReadOnlyDictionary<string, string> Metadata => _metadata;

    public void AddComment(string comment, string author)
    {
        _comments.Add(new AlertComment(Id, comment, author));
    }

    public void Acknowledge(string? acknowledgedBy = null)
    {
        if (Status == AlertStatus.Open)
        {
            Status = AlertStatus.Acknowledged;
            AcknowledgedAt = DateTimeOffset.UtcNow;
            AcknowledgedBy = acknowledgedBy;
        }
    }

    public void Resolve(string? resolution = null, string? resolvedBy = null)
    {
        Status = AlertStatus.Resolved;
        Resolution = resolution;
        ResolvedAt = DateTimeOffset.UtcNow;
        ResolvedBy = resolvedBy;
    }

    public void AssignTo(string assignedTo)
    {
        AssignedTo = assignedTo;
    }

    public void SetStatus(AlertStatus newStatus)
    {
        Status = newStatus;
    }
}

public class AlertComment : BaseEntity
{
    protected AlertComment() : base() { }

    public AlertComment(Guid alertId, string comment, string author) : base()
    {
        AlertId = alertId;
        Comment = comment;
        Author = author;
        Timestamp = DateTimeOffset.UtcNow;
    }

    public Guid AlertId { get; private set; }
    public string Comment { get; private set; }
    public string Author { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
}
