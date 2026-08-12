namespace Sentinela.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
[RequirePermission("dashboard.view")]
public class DashboardController : ControllerBase
{
    private readonly IRepository<Computer> _computerRepo;
    private readonly IRepository<TimelineEntry> _timelineRepo;
    private readonly IRepository<Alert> _alertRepo;
    private readonly IRepository<ApplicationUsage> _appUsageRepo;
    private readonly IRepository<Heartbeat> _heartbeatRepo;
    private readonly ICacheService _cache;
    private readonly IMapper _mapper;
    private readonly ILogger<DashboardController> _logger;
    private readonly ITenantAccessor _tenantAccessor;

    public DashboardController(
        IRepository<Computer> computerRepo,
        IRepository<TimelineEntry> timelineRepo,
        IRepository<Alert> alertRepo,
        IRepository<ApplicationUsage> appUsageRepo,
        IRepository<Heartbeat> heartbeatRepo,
        ICacheService cache,
        IMapper mapper,
        ILogger<DashboardController> logger,
        ITenantAccessor tenantAccessor)
    {
        _computerRepo = computerRepo;
        _timelineRepo = timelineRepo;
        _alertRepo = alertRepo;
        _appUsageRepo = appUsageRepo;
        _heartbeatRepo = heartbeatRepo;
        _cache = cache;
        _mapper = mapper;
        _logger = logger;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet("overview")]
    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> GetOverview()
    {
        var tenantId = _tenantAccessor.TenantId;
        var stats = await _cache.GetOrCreateAsync($"dashboard:overview:{tenantId}", async () =>
        {
            var computers = _computerRepo.Query().Where(c => !c.IsDeleted && c.TenantId == tenantId);
            var alerts = _alertRepo.Query().Where(a => !a.IsDeleted && a.TenantId == tenantId);

            return new DashboardStatsDto
            {
                TotalComputers = await computers.CountAsync(),
                OnlineComputers = await computers.CountAsync(c => c.Status == ComputerStatus.Online),
                OfflineComputers = await computers.CountAsync(c => c.Status == ComputerStatus.Offline),
                TotalUsers = await computers.Select(c => c.CurrentUser).Distinct().CountAsync(),
                TotalDepartments = await computers.Select(c => c.Department).Distinct().CountAsync(),
                TotalAlerts = await alerts.CountAsync(a => a.Status == AlertStatus.Open),
                CriticalAlerts = await alerts.CountAsync(a => a.Status == AlertStatus.Open && a.Severity == Severity.Critical),
                HighAlerts = await alerts.CountAsync(a => a.Status == AlertStatus.Open && a.Severity == Severity.High)
            };
        }, TimeSpan.FromSeconds(30));

        return Ok(stats);
    }

    [HttpGet("heatmap")]
    public async Task<ActionResult<List<HeatmapDto>>> GetHeatmap([FromQuery] int days = 7)
    {
        var tenantId = _tenantAccessor.TenantId;
        var from = DateTime.UtcNow.AddDays(-days);
        var data = await _timelineRepo.Query()
            .Where(t => t.Timestamp >= from && !t.IsDeleted && t.TenantId == tenantId)
            .GroupBy(t => t.Timestamp.Hour)
            .Select(g => new HeatmapDto
            {
                Date = DateTime.UtcNow.Date,
                Hour = g.Key,
                Count = g.Count()
            })
            .OrderBy(h => h.Hour)
            .ToListAsync();

        return Ok(data);
    }

    [HttpGet("top-applications")]
    public async Task<ActionResult<List<ApplicationUsageDto>>> GetTopApplications([FromQuery] int top = 10, [FromQuery] int days = 7)
    {
        var tenantId = _tenantAccessor.TenantId;
        var from = DateTime.UtcNow.AddDays(-days);
        var rawApps = await _appUsageRepo.Query()
            .Where(a => a.StartTime >= from && a.TenantId == tenantId)
            .ToListAsync();

        var apps = rawApps
            .GroupBy(a => a.ProcessName)
            .Select(g => new ApplicationUsageDto
            {
                ProcessName = g.Key,
                TotalDuration = (long)g.Sum(a => a.Duration?.TotalMilliseconds ?? 0),
                ExecutionCount = g.Count(),
                FirstSeen = g.Min(a => a.StartTime),
                LastSeen = g.Max(a => a.EndTime ?? a.StartTime)
            })
            .OrderByDescending(a => a.TotalDuration)
            .Take(top)
            .ToList();

        return Ok(apps);
    }

    [HttpGet("top-users")]
    public async Task<ActionResult<List<TopUserDto>>> GetTopUsers([FromQuery] int top = 10, [FromQuery] int days = 7)
    {
        var tenantId = _tenantAccessor.TenantId;
        var from = DateTime.UtcNow.AddDays(-days);
        var users = await _timelineRepo.Query()
            .Where(t => t.Timestamp >= from && !t.IsDeleted && t.Username != null && t.TenantId == tenantId)
            .GroupBy(t => t.Username)
            .Select(g => new TopUserDto
            {
                Username = g.Key,
                EventCount = g.Count(),
                LastActivity = g.Max(t => t.Timestamp.DateTime)
            })
            .OrderByDescending(u => u.EventCount)
            .Take(top)
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("recent-events")]
    public async Task<ActionResult<List<TimelineEntryDto>>> GetRecentEvents([FromQuery] int limit = 20)
    {
        var tenantId = _tenantAccessor.TenantId;
        var events = await _timelineRepo.Query()
            .Where(t => !t.IsDeleted && t.TenantId == tenantId)
            .OrderByDescending(t => t.Timestamp)
            .Take(limit)
            .ToListAsync();

        return Ok(_mapper.Map<List<TimelineEntryDto>>(events));
    }

    [HttpGet("activity")]
    public async Task<ActionResult<List<TimelineEntryDto>>> GetActivity([FromQuery] int limit = 100)
    {
        var tenantId = _tenantAccessor.TenantId;
        var events = await _timelineRepo.Query()
            .Where(t => !t.IsDeleted && t.TenantId == tenantId)
            .OrderByDescending(t => t.Timestamp)
            .Take(limit)
            .ToListAsync();

        var computerIds = events.Select(e => e.ComputerId).Distinct().ToList();
        var computers = await _computerRepo.Query()
            .Where(c => computerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Hostname);

        var dtos = events.Select(e => new TimelineEntryDto
        {
            Id = e.Id,
            ComputerId = e.ComputerId,
            EventType = e.EventType.ToString(),
            Category = e.Category,
            Description = e.Description,
            Details = e.Details,
            Username = e.Username ?? "",
            Severity = e.Severity.ToString(),
            Timestamp = e.Timestamp.UtcDateTime,
            ComputerName = computers.GetValueOrDefault(e.ComputerId)
        }).ToList();

        return Ok(dtos);
    }

    [HttpGet("availability")]
    public async Task<ActionResult<List<AvailabilityDto>>> GetAvailability([FromQuery] int days = 30)
    {
        var tenantId = _tenantAccessor.TenantId;
        var from = DateTime.UtcNow.AddDays(-days);
        var data = await _heartbeatRepo.Query()
            .Where(h => h.Timestamp >= from && h.TenantId == tenantId)
            .GroupBy(h => h.Timestamp.Date)
            .Select(g => new AvailabilityDto
            {
                Date = g.Key,
                OnlineCount = g.Count(h => h.Status == ComputerStatus.Online),
                TotalCount = g.Count()
            })
            .OrderBy(a => a.Date)
            .ToListAsync();

        return Ok(data);
    }

    [HttpGet("security-overview")]
    public async Task<ActionResult<SecurityOverviewDto>> GetSecurityOverview()
    {
        var tenantId = _tenantAccessor.TenantId;
        var overview = await _cache.GetOrCreateAsync($"dashboard:security-overview:{tenantId}", async () =>
        {
            var alerts = _alertRepo.Query().Where(a => !a.IsDeleted && a.TenantId == tenantId);
            return new SecurityOverviewDto
            {
                OpenAlerts = await alerts.CountAsync(a => a.Status == AlertStatus.Open),
                AcknowledgedAlerts = await alerts.CountAsync(a => a.Status == AlertStatus.Acknowledged),
                ResolvedAlerts = await alerts.CountAsync(a => a.Status == AlertStatus.Resolved),
                CriticalAlerts = await alerts.CountAsync(a => a.Severity == Severity.Critical && a.Status != AlertStatus.Resolved),
                HighAlerts = await alerts.CountAsync(a => a.Severity == Severity.High && a.Status != AlertStatus.Resolved),
                MediumAlerts = await alerts.CountAsync(a => a.Severity == Severity.Medium && a.Status != AlertStatus.Resolved),
                LowAlerts = await alerts.CountAsync(a => a.Severity == Severity.Low && a.Status != AlertStatus.Resolved),
                AvgResponseTime = await alerts
                    .Where(a => a.Status == AlertStatus.Resolved && a.ResolvedAt != null)
                    .AverageAsync(a => (a.ResolvedAt!.Value - a.CreatedAt).TotalMinutes)
            };
        }, TimeSpan.FromSeconds(30));

        return Ok(overview);
    }
}
