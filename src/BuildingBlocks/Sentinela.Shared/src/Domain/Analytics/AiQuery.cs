namespace Sentinela.Shared.Domain.Analytics;

public class AiQuery
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string? Response { get; set; }
    public QueryType Type { get; set; }
    public QueryStatus Status { get; set; } = QueryStatus.Pending;
    public DateTimeOffset AskedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AnsweredAt { get; set; }
    public TimeSpan? ProcessingTime { get; set; }
    public bool WasHelpful { get; set; }
    public string? Feedback { get; set; }
    public Dictionary<string, string> Context { get; set; } = new();
}

public enum QueryType
{
    NaturalLanguage,
    Structured,
    Report,
    AlertSummary,
    Recommendation,
    Investigation
}

public enum QueryStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    NeedsClarification
}
