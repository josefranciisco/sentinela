using Sentinela.Shared.Core.Entities;

namespace Sentinela.Shared.Domain.Security;

public class CorrelationRule : AggregateRoot
{
    private readonly List<string> _tags = new();
    private readonly List<string> _actions = new();
    private readonly Dictionary<string, int> _minCounts = new();

    protected CorrelationRule() : base() { }

    public CorrelationRule(
        string name,
        string conditionExpression,
        TimeSpan timeWindow,
        double score = 0,
        int priority = 0,
        string? description = null)
        : base()
    {
        Name = name;
        Description = description;
        IsEnabled = true;
        TimeWindow = timeWindow;
        ConditionExpression = conditionExpression;
        Score = score;
        Priority = priority;
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }
    public bool IsEnabled { get; private set; }
    public TimeSpan TimeWindow { get; private set; }
    public string ConditionExpression { get; private set; }
    public double Score { get; private set; }
    public int Priority { get; private set; }
    public IReadOnlyDictionary<string, int> MinCounts => _minCounts;

    public IReadOnlyList<string> Tags => _tags.AsReadOnly();
    public IReadOnlyList<string> Actions => _actions.AsReadOnly();

    public void Enable() => IsEnabled = true;
    public void Disable() => IsEnabled = false;

    public void Update(
        string name,
        string? description,
        string conditionExpression,
        int priority,
        double score,
        IReadOnlyDictionary<string, int>? minCounts,
        TimeSpan timeWindow,
        IReadOnlyList<string>? tags)
    {
        Name = name;
        Description = description;
        ConditionExpression = conditionExpression;
        Priority = priority;
        Score = score;
        TimeWindow = timeWindow;

        _minCounts.Clear();
        if (minCounts != null)
        {
            foreach (var kv in minCounts)
                _minCounts[kv.Key] = kv.Value;
        }

        _tags.Clear();
        if (tags != null)
        {
            foreach (var tag in tags)
                _tags.Add(tag);
        }
    }

    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
    }
}
