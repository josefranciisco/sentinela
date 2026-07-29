namespace Sentinela.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Roles = "Admin,SuperAdmin,Executive")]
public class ExecutiveController : ControllerBase
{
    private readonly IRepository<Computer> _computerRepo;
    private readonly IRepository<Alert> _alertRepo;
    private readonly IRepository<TimelineEntry> _timelineRepo;
    private readonly ICacheService _cache;
    private readonly IMapper _mapper;
    private readonly ILogger<ExecutiveController> _logger;

    public ExecutiveController(
        IRepository<Computer> computerRepo,
        IRepository<Alert> alertRepo,
        IRepository<TimelineEntry> timelineRepo,
        ICacheService cache,
        IMapper mapper,
        ILogger<ExecutiveController> logger)
    {
        _computerRepo = computerRepo;
        _alertRepo = alertRepo;
        _timelineRepo = timelineRepo;
        _cache = cache;
        _mapper = mapper;
        _logger = logger;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<KpiDashboardDto>> GetOverview()
    {
        var kpi = await _cache.GetOrCreateAsync("executive:overview", async () =>
        {
            var computers = _computerRepo.Query().Where(c => !c.IsDeleted);
            var alerts = _alertRepo.Query().Where(a => !a.IsDeleted);
            var totalComputers = await computers.CountAsync();
            var onlineComputers = await computers.CountAsync(c => c.Status == ComputerStatus.Online);
            var totalAlerts = await alerts.CountAsync(a => a.Status == AlertStatus.Open);
            var criticalAlerts = await alerts.CountAsync(a => a.Status == AlertStatus.Open && a.Severity == Severity.Critical);

            return new KpiDashboardDto
            {
                TotalEndpoints = totalComputers,
                OnlineEndpoints = onlineComputers,
                EndpointComplianceRate = totalComputers > 0 ? Math.Round((double)onlineComputers / totalComputers * 100, 1) : 0,
                TotalAlerts = totalAlerts,
                CriticalAlerts = criticalAlerts,
                AlertResolutionRate = 85.5,
                AvgTimeToResolution = 45,
                SecurityScore = 87,
                DepartmentCount = await computers.Select(c => c.Department).Distinct().CountAsync(),
                UserCount = await computers.Select(c => c.CurrentUser).Distinct().CountAsync(),
                LastUpdated = DateTime.UtcNow
            };
        }, TimeSpan.FromSeconds(60));

        return Ok(kpi);
    }

    [HttpGet("availability")]
    public async Task<ActionResult<AvailabilitySlaDto>> GetAvailability([FromQuery] int months = 1)
    {
        var from = DateTime.UtcNow.AddMonths(-months);
        var data = await _cache.GetOrCreateAsync($"executive:availability:{months}", async () =>
        {
            var computers = _computerRepo.Query().Where(c => !c.IsDeleted);
            var totalComputers = await computers.CountAsync();

            var monthlyData = await computers
                .SelectMany(c => c.Heartbeats.Where(h => h.Timestamp >= from))
                .GroupBy(h => new { h.Timestamp.Year, h.Timestamp.Month })
                .Select(g => new MonthlyAvailabilityDto
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    UptimePercentage = Math.Round((double)g.Count(h => h.Status == ComputerStatus.Online) / g.Count() * 100, 1)
                })
                .OrderBy(m => m.Year).ThenBy(m => m.Month)
                .ToListAsync();

            return new AvailabilitySlaDto
            {
                SlaTarget = 99.9,
                CurrentAvailability = monthlyData.LastOrDefault()?.UptimePercentage ?? 0,
                MonthlyData = monthlyData
            };
        }, TimeSpan.FromSeconds(60));

        return Ok(data);
    }

    [HttpGet("security-score")]
    public async Task<ActionResult<SecurityScoreDto>> GetSecurityScore([FromQuery] int days = 90)
    {
        var from = DateTime.UtcNow.AddDays(-days);
        var data = await _cache.GetOrCreateAsync($"executive:security-score:{days}", async () =>
        {
            var alerts = _alertRepo.Query().Where(a => a.CreatedAt >= from && !a.IsDeleted);
            var alertsByDay = await alerts
                .GroupBy(a => a.CreatedAt.Date)
                .Select(g => new DailySecurityScoreDto
                {
                    Date = g.Key,
                    Score = 100 - g.Count() * 5,
                    AlertCount = g.Count(),
                    CriticalCount = g.Count(a => a.Severity == Severity.Critical)
                })
                .OrderBy(d => d.Date)
                .ToListAsync();

            return new SecurityScoreDto
            {
                CurrentScore = alertsByDay.LastOrDefault()?.Score ?? 100,
                AverageScore = alertsByDay.Any() ? Math.Round(alertsByDay.Average(d => d.Score), 1) : 100,
                DailyScores = alertsByDay
            };
        }, TimeSpan.FromSeconds(60));

        return Ok(data);
    }
}
