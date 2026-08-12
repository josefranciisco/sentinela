using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentinela.Api.Services;


namespace Sentinela.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
[RequirePermission("security.view")]
public class AiController : ControllerBase
{
    private readonly IAiAssistantService _aiService;
    private readonly ILogger<AiController> _logger;

    public AiController(IAiAssistantService aiService, ILogger<AiController> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    [HttpPost("ask")]
    public async Task<ActionResult<AiResponse>> Ask([FromBody] AiQueryRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
        var userName = User.Identity?.Name ?? "Unknown";

        var response = await _aiService.AskAsync(request.Query, userId, userName, request.Context);
        return Ok(response);
    }

    [HttpPost("analyze/{computerId}")]
    public async Task<ActionResult<AiResponse>> AnalyzeComputer(Guid computerId)
    {
        var response = await _aiService.AnalyzeComputerAsync(computerId);
        return Ok(response);
    }

    [HttpGet("alerts/{alertId}/explain")]
    public async Task<ActionResult<AiResponse>> ExplainAlert(Guid alertId)
    {
        var response = await _aiService.ExplainAlertAsync(alertId);
        return Ok(response);
    }

    [HttpPost("suggest/{computerId}")]
    public async Task<ActionResult<AiResponse>> SuggestActions(Guid computerId, [FromBody] string? issue = null)
    {
        var response = await _aiService.SuggestActionsAsync(computerId, issue);
        return Ok(response);
    }

    [HttpPost("report")]
    public async Task<ActionResult<AiResponse>> GenerateReport([FromBody] ReportRequest request)
    {
        var response = await _aiService.GenerateReportAsync(request.Type, request.Parameters);
        return Ok(response);
    }

    [HttpGet("insights")]
    public async Task<ActionResult<List<DashboardInsight>>> GetInsights()
    {
        var insights = await _aiService.GenerateInsightsAsync();
        return Ok(insights);
    }

    [HttpPost("summarize")]
    public async Task<ActionResult<AiResponse>> Summarize([FromBody] SummarizeRequest request)
    {
        var response = await _aiService.SummarizeEventsAsync(request.From, request.To, request.ComputerId);
        return Ok(response);
    }

    [HttpGet("prioritize")]
    public async Task<ActionResult<AiResponse>> PrioritizeIncidents()
    {
        var response = await _aiService.PrioritizeIncidentsAsync();
        return Ok(response);
    }
}

public record AiQueryRequest(string Query, Dictionary<string, string>? Context = null);
public record ReportRequest(ReportType Type, Dictionary<string, object> Parameters);
public record SummarizeRequest(DateTimeOffset From, DateTimeOffset To, string? ComputerId = null);
