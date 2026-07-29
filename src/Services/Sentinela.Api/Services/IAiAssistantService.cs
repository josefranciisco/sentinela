namespace Sentinela.Api.Services;

public interface IAiAssistantService
{
    Task<AiResponse> AskAsync(string query, Guid userId, string userName, Dictionary<string, string>? context = null);
    Task<AiResponse> AnalyzeComputerAsync(Guid computerId);
    Task<AiResponse> GenerateReportAsync(ReportType type, Dictionary<string, object> parameters);
    Task<List<DashboardInsight>> GenerateInsightsAsync();
    Task<AiResponse> ExplainAlertAsync(Guid alertId);
    Task<AiResponse> SuggestActionsAsync(Guid computerId, string? issue = null);
    Task<AiResponse> SummarizeEventsAsync(DateTimeOffset from, DateTimeOffset to, string? computerId = null);
    Task<AiResponse> PrioritizeIncidentsAsync();
}

public class AiResponse
{
    public Guid QueryId { get; set; }
    public string Text { get; set; } = string.Empty;
    public List<AiAction> SuggestedActions { get; set; } = new();
    public List<AiChart> Charts { get; set; } = new();
    public List<AiReference> References { get; set; } = new();
    public TimeSpan ProcessingTime { get; set; }
}

public class AiAction
{
    public string Label { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class AiChart
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public object Data { get; set; } = new();
}

public class AiReference
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class DashboardInsight
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Recommendation { get; set; } = string.Empty;
    public Dictionary<string, object>? Metadata { get; set; }
}
