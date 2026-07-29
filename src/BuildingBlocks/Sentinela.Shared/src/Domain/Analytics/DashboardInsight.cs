using Sentinela.Shared.Domain.Monitoring.Enums;

namespace Sentinela.Shared.Domain.Analytics;

public class DashboardInsight
{
    public Guid Id { get; set; }
    public InsightCategory Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Severity Severity { get; set; } = Severity.Info;
    public string? ActionUrl { get; set; }
    public string? ActionLabel { get; set; }
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool IsDismissed { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}

public enum InsightCategory
{
    Anomaly,
    Trend,
    Performance,
    Security,
    Compliance,
    Suggestion,
    Alert
}
