public enum ReportType
{
    DailySummary,
    WeeklyReport,
    MonthlyReport,
    ExecutiveSummary,
    SecurityReport,
    ComplianceReport,
    Custom
}

public class Report
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ReportType Type { get; set; }
    public string? Description { get; set; }
    public string? Template { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public DateTimeOffset GeneratedAt { get; set; }
    public string GeneratedBy { get; set; } = string.Empty;
    public string? Content { get; set; }
    public string? StoragePath { get; set; }
    public bool IsScheduled { get; set; }
    public string? ScheduleCron { get; set; }
    public string[] Recipients { get; set; } = Array.Empty<string>();
}
