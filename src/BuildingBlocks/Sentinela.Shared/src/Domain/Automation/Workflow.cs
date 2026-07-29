using Sentinela.Shared.Core.Entities;

namespace Sentinela.Shared.Domain.Automation;

public class Workflow : AggregateRoot
{
    private readonly List<WorkflowCondition> _conditions = new();
    private readonly List<WorkflowAction> _actions = new();

    protected Workflow() : base() { }

    public Workflow(
        string name,
        string? description,
        string triggerType,
        string? triggerConfig = null,
        string? createdBy = null)
        : base()
    {
        Name = name;
        Description = description;
        IsEnabled = true;
        TriggerType = triggerType;
        TriggerConfig = triggerConfig;
        CreatedBy = createdBy;
        CreatedAt = DateTimeOffset.UtcNow;
        ExecutionCount = 0;
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }
    public bool IsEnabled { get; private set; }
    public string TriggerType { get; private set; }
    public string? TriggerConfig { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public int ExecutionCount { get; private set; }
    public DateTimeOffset? LastExecutedAt { get; private set; }
    public string? CronExpression { get; private set; }

    public IReadOnlyList<WorkflowCondition> Conditions => _conditions.AsReadOnly();
    public IReadOnlyList<WorkflowAction> Actions => _actions.AsReadOnly();

    public void Enable() => IsEnabled = true;
    public void Disable() => IsEnabled = false;

    public void RecordExecution()
    {
        ExecutionCount++;
        LastExecutedAt = DateTimeOffset.UtcNow;
    }

    public void AddCondition(string field, WorkflowCondition.ComparisonOperator @operator, string value)
    {
        _conditions.Add(new WorkflowCondition(field, @operator, value));
    }

    public void AddAction(ActionType type, string? config = null, int order = 0)
    {
        _actions.Add(new WorkflowAction(type, config, order));
    }

    public void UpdateDetails(string name, string? description, string triggerType)
    {
        Name = name;
        Description = description;
        TriggerType = triggerType;
    }
}
