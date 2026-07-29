using Sentinela.Shared.Core.Entities;
using Sentinela.Shared.Domain.Monitoring.Enums;

namespace Sentinela.Shared.Domain.Alerting;

public class AlertRule : AggregateRoot
{
    private readonly List<string> _tags = new();

    protected AlertRule() : base() { }

    public AlertRule(
        string name,
        string condition,
        Severity severity,
        string? description = null,
        string? category = null,
        string? createdBy = null)
        : base()
    {
        Name = name;
        Description = description;
        Category = category;
        Severity = severity;
        IsEnabled = true;
        Condition = condition;
        CreatedBy = createdBy;
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string? Category { get; private set; }
    public Severity Severity { get; private set; }
    public bool IsEnabled { get; private set; }
    public string Condition { get; private set; }
    public TimeSpan? EvaluationWindow { get; private set; }
    public TimeSpan? CooldownPeriod { get; private set; }
    public int? MaxNotifications { get; private set; }
    public string? CreatedBy { get; private set; }

    public IReadOnlyList<string> Tags => _tags.AsReadOnly();

    public void Enable() => IsEnabled = true;
    public void Disable() => IsEnabled = false;
}
