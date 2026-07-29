using Sentinela.Shared.Core.Entities;

namespace Sentinela.Shared.Domain.Automation;

public class WorkflowExecutionLog : BaseEntity
{
    protected WorkflowExecutionLog() : base() { }

    public WorkflowExecutionLog(
        Guid workflowId,
        string triggeredBy,
        string status,
        string? details = null)
        : base()
    {
        WorkflowId = workflowId;
        TriggeredBy = triggeredBy;
        Status = status;
        StartedAt = DateTimeOffset.UtcNow;
        Details = details;
    }

    public Guid WorkflowId { get; private set; }
    public string TriggeredBy { get; private set; }
    public string Status { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? Details { get; private set; }
}
