using Sentinela.Shared.Core.Entities;

namespace Sentinela.Shared.Domain.Automation;

public class WorkflowCondition : BaseEntity
{
    protected WorkflowCondition() : base() { }

    public WorkflowCondition(string field, ComparisonOperator @operator, string value)
        : base()
    {
        Field = field;
        Operator = @operator;
        Value = value;
    }

    public string Field { get; private set; }
    public ComparisonOperator Operator { get; private set; }
    public string Value { get; private set; }

    public enum ComparisonOperator
    {
        Equals,
        NotEquals,
        GreaterThan,
        LessThan,
        Contains,
        StartsWith,
        EndsWith,
        In,
        NotIn,
        Between,
        Regex
    }
}
