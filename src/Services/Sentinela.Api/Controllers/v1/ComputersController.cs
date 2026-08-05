namespace Sentinela.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class ComputersController : ControllerBase
{
    private readonly IRepository<Computer> _computerRepo;
    private readonly IRepository<TimelineEntry> _timelineRepo;
    private readonly IRepository<ApplicationUsage> _appUsageRepo;
    private readonly IRepository<Alert> _alertRepo;
    private readonly IRepository<EndpointSecurityStatus> _securityStatusRepo;
    private readonly IEventBus _eventBus;
    private readonly ICacheService _cache;
    private readonly IMapper _mapper;
    private readonly ILogger<ComputersController> _logger;

    public ComputersController(
        IRepository<Computer> computerRepo,
        IRepository<TimelineEntry> timelineRepo,
        IRepository<ApplicationUsage> appUsageRepo,
        IRepository<Alert> alertRepo,
        IRepository<EndpointSecurityStatus> securityStatusRepo,
        IEventBus eventBus,
        ICacheService cache,
        IMapper mapper,
        ILogger<ComputersController> logger)
    {
        _computerRepo = computerRepo;
        _timelineRepo = timelineRepo;
        _appUsageRepo = appUsageRepo;
        _alertRepo = alertRepo;
        _securityStatusRepo = securityStatusRepo;
        _eventBus = eventBus;
        _cache = cache;
        _mapper = mapper;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResult<ComputerDto>>> GetComputers(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? department = null,
        [FromQuery] string? sortBy = "hostname",
        [FromQuery] string? sortDirection = "asc")
    {
        var query = _computerRepo.Query().Where(c => !c.IsDeleted);
        
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Hostname.Contains(search) || c.IpAddress.Contains(search) || c.CurrentUser.Contains(search));
        
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ComputerStatus>(status, true, out var computerStatus))
            query = query.Where(c => c.Status == computerStatus);
        
        if (!string.IsNullOrWhiteSpace(department))
            query = query.Where(c => c.Department == department);
        
        query = sortBy.ToLower() switch
        {
            "hostname" => sortDirection == "desc" ? query.OrderByDescending(c => c.Hostname) : query.OrderBy(c => c.Hostname),
            "status" => sortDirection == "desc" ? query.OrderByDescending(c => c.Status) : query.OrderBy(c => c.Status),
            "lastheartbeat" => sortDirection == "desc" ? query.OrderByDescending(c => c.LastHeartbeat) : query.OrderBy(c => c.LastHeartbeat),
            "department" => sortDirection == "desc" ? query.OrderByDescending(c => c.Department) : query.OrderBy(c => c.Department),
            _ => query.OrderBy(c => c.Hostname)
        };
        
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        
        return Ok(new PaginatedResult<ComputerDto>
        {
            Items = _mapper.Map<List<ComputerDto>>(items),
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ComputerDetailDto>> GetComputer(Guid id)
    {
        var computer = await _computerRepo.GetByIdAsync(id);
        if (computer is null) return NotFound();

        var dto = _mapper.Map<ComputerDetailDto>(computer);

        var status = await _securityStatusRepo.Query()
            .Where(s => s.ComputerId == id && !s.IsDeleted)
            .OrderByDescending(s => s.CollectedAt)
            .FirstOrDefaultAsync();

        if (status is not null)
        {
            dto.FirewallEnabled = status.FirewallEnabled;
            dto.DefenderEnabled = status.DefenderEnabled || status.RealTimeProtectionEnabled;
            dto.AntivirusEnabled = status.AntivirusEnabled;
            dto.RealTimeProtectionEnabled = status.RealTimeProtectionEnabled;
            dto.BitlockerEnabled = status.BitlockerEnabled;
            dto.RdpEnabled = status.RdpEnabled;
            dto.AntivirusProductName = status.AntivirusProductName;
            dto.SecurityCollectedAt = status.CollectedAt.UtcDateTime;
        }

        return Ok(dto);
    }

    [HttpGet("{id}/timeline")]
    public async Task<ActionResult<PaginatedResult<TimelineEntryDto>>> GetTimeline(
        Guid id,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? eventType = null,
        [FromQuery] string? username = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = _timelineRepo.Query()
            .Where(t => t.ComputerId == id && !t.IsDeleted);
        
        if (from.HasValue) query = query.Where(t => t.Timestamp >= from.Value);
        if (to.HasValue) query = query.Where(t => t.Timestamp <= to.Value);
        if (!string.IsNullOrWhiteSpace(eventType) && Enum.TryParse<EventType>(eventType, true, out var evtType))
            query = query.Where(t => t.EventType == evtType);
        if (!string.IsNullOrWhiteSpace(username))
            query = query.Where(t => t.Username == username);
        
        query = query.OrderByDescending(t => t.Timestamp);
        
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        
        return Ok(new PaginatedResult<TimelineEntryDto>
        {
            Items = _mapper.Map<List<TimelineEntryDto>>(items),
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    [HttpGet("{id}/applications")]
    public async Task<ActionResult<List<ApplicationUsageDto>>> GetApplications(
        Guid id,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int top = 20)
    {
        var rawApps = await _appUsageRepo.Query()
            .Where(a => a.ComputerId == id)
            .ToListAsync();

        var apps = rawApps
            .GroupBy(a => a.ProcessName)
            .Select(g => new ApplicationUsageDto
            {
                ProcessName = g.Key,
                TotalDuration = (long)g.Sum(a => a.Duration?.TotalMilliseconds ?? 0),
                ExecutionCount = g.Count(),
                FirstSeen = g.Min(a => a.StartTime),
                LastSeen = g.Max(a => a.EndTime) ?? g.Min(a => a.StartTime)
            })
            .OrderByDescending(a => a.TotalDuration)
            .Take(top)
            .ToList();
        
        return Ok(apps);
    }

    [HttpPost("{id}/command")]
    [Authorize(Roles = "Admin,SuperAdmin,Operator")]
    public async Task<IActionResult> SendCommand(Guid id, [FromBody] AgentCommandDto command)
    {
        var agentCommand = new AgentCommand
        {
            ComputerId = id,
            CommandType = command.Type,
            Payload = command.Payload,
            IssuedBy = User.Identity.Name,
            IssuedAt = DateTime.UtcNow
        };
        
        await _eventBus.PublishAsync(new AgentCommandEvent(id, command.Type, command.Payload, User.Identity.Name));
        return Accepted();
    }

    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> GetStats()
    {
        var stats = await _cache.GetOrCreateAsync("dashboard:stats", async () =>
        {
            var computers = _computerRepo.Query();
            return new DashboardStatsDto
            {
                TotalComputers = await computers.CountAsync(),
                OnlineComputers = await computers.CountAsync(c => c.Status == ComputerStatus.Online),
                OfflineComputers = await computers.CountAsync(c => c.Status == ComputerStatus.Offline),
                TotalUsers = await computers.Select(c => c.CurrentUser).Distinct().CountAsync(),
                TotalDepartments = await computers.Select(c => c.Department).Distinct().CountAsync(),
                TotalAlerts = await _alertRepo.Query().CountAsync(a => a.Status == AlertStatus.Open),
                CriticalAlerts = await _alertRepo.Query().CountAsync(a => a.Status == AlertStatus.Open && a.Severity == Severity.Critical)
            };
        }, TimeSpan.FromSeconds(30));
        
        return Ok(stats);
    }
}
