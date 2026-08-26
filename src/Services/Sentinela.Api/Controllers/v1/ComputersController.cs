namespace Sentinela.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
[RequirePermission("machines.view")]
public class ComputersController : ControllerBase
{
    private readonly IRepository<Computer> _computerRepo;
    private readonly IRepository<TimelineEntry> _timelineRepo;
    private readonly IRepository<ApplicationUsage> _appUsageRepo;
    private readonly IRepository<Alert> _alertRepo;
    private readonly IRepository<EndpointSecurityStatus> _securityStatusRepo;
    private readonly IRepository<SoftwareInventoryItem> _softwareRepo;
    private readonly IEventBus _eventBus;
    private readonly ICacheService _cache;
    private readonly IMapper _mapper;
    private readonly ILogger<ComputersController> _logger;
    private readonly ITenantAccessor _tenantAccessor;
    private readonly IHubContext<AgentHub> _agentHub;

    public ComputersController(
        IRepository<Computer> computerRepo,
        IRepository<TimelineEntry> timelineRepo,
        IRepository<ApplicationUsage> appUsageRepo,
        IRepository<Alert> alertRepo,
        IRepository<EndpointSecurityStatus> securityStatusRepo,
        IRepository<SoftwareInventoryItem> softwareRepo,
        IEventBus eventBus,
        ICacheService cache,
        IMapper mapper,
        ILogger<ComputersController> logger,
        ITenantAccessor tenantAccessor,
        IHubContext<AgentHub> agentHub)
    {
        _computerRepo = computerRepo;
        _timelineRepo = timelineRepo;
        _appUsageRepo = appUsageRepo;
        _alertRepo = alertRepo;
        _securityStatusRepo = securityStatusRepo;
        _softwareRepo = softwareRepo;
        _eventBus = eventBus;
        _cache = cache;
        _mapper = mapper;
        _logger = logger;
        _tenantAccessor = tenantAccessor;
        _agentHub = agentHub;
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
        var tenantId = _tenantAccessor.TenantId;
        var query = _computerRepo.Query()
            .Where(c => !c.IsDeleted && c.TenantId == tenantId);
        
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Hostname.Contains(search) || c.IpAddress.Contains(search) || c.CurrentUser.Contains(search));
        
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ComputerStatus>(status, true, out var computerStatus))
            query = query.Where(c => c.Status == computerStatus);
        
        if (!string.IsNullOrWhiteSpace(department))
            query = query.Where(c => c.Department == department);

        var sortKey = (sortBy ?? "hostname").ToLowerInvariant();
        var desc = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        // Hostname usa ordem natural (Mobi-01, Mobi-02, Mobi-10) — exige materializar
        if (sortKey is "hostname" or "")
        {
            var all = await query.ToListAsync();
            var ordered = desc
                ? all.OrderByDescending(c => c.Hostname, NaturalStringComparer.Instance)
                : all.OrderBy(c => c.Hostname, NaturalStringComparer.Instance);

            var totalNatural = all.Count;
            var pageItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return Ok(new PaginatedResult<ComputerDto>
            {
                Items = _mapper.Map<List<ComputerDto>>(pageItems),
                Total = totalNatural,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalNatural / (double)pageSize)
            });
        }

        query = sortKey switch
        {
            "status" => desc ? query.OrderByDescending(c => c.Status) : query.OrderBy(c => c.Status),
            "lastheartbeat" => desc ? query.OrderByDescending(c => c.LastHeartbeat) : query.OrderBy(c => c.LastHeartbeat),
            "department" => desc ? query.OrderByDescending(c => c.Department) : query.OrderBy(c => c.Department),
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
        var tenantId = _tenantAccessor.TenantId;
        var computer = await _computerRepo.Query()
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
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
            dto.AntivirusSignatureAgeDays = status.AntivirusSignatureAgeDays;
            dto.AntivirusSignatureLastUpdated = status.AntivirusSignatureLastUpdated?.UtcDateTime;
            dto.SecurityCollectedAt = status.CollectedAt.UtcDateTime;
        }

        return Ok(dto);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateComputer(Guid id, [FromBody] UpdateComputerDto dto)
    {
        var tenantId = _tenantAccessor.TenantId;
        var computer = await _computerRepo.Query()
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
        if (computer is null) return NotFound();

        var changed = false;
        if (!string.IsNullOrWhiteSpace(dto.Hostname) && dto.Hostname != computer.Hostname)
        {
            computer.UpdateHostname(dto.Hostname);
            changed = true;
        }
        if (dto.Department is not null && dto.Department != computer.Department)
        {
            computer.UpdateDepartment(dto.Department.Trim());
            changed = true;
        }

        if (changed)
        {
            await _computerRepo.SaveChangesAsync();
            _logger.LogInformation("Computer {Id} ({Hostname}) updated by {User}", id, computer.Hostname, User.Identity?.Name);
        }

        return Ok(_mapper.Map<ComputerDto>(computer));
    }

    [HttpDelete("{id}")]
    [RequirePermission("machines.delete")]
    public async Task<IActionResult> DeleteComputer(Guid id)
    {
        var tenantId = _tenantAccessor.TenantId;
        var computer = await _computerRepo.Query()
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted);
        if (computer is null) return NotFound();

        await _computerRepo.DeleteAsync(computer);
        _logger.LogInformation("Computer {Id} ({Hostname}) deleted by {User}", id, computer.Hostname, User.Identity?.Name);
        return NoContent();
    }

    public async Task<ActionResult<List<ComputerSoftwareItemDto>>> GetSoftware(
        Guid id,
        [FromQuery] string? search = null)
    {
        var tenantId = _tenantAccessor.TenantId;
        var exists = await _computerRepo.Query()
            .AnyAsync(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted);
        if (!exists) return NotFound();

        var query = _softwareRepo.Query()
            .Where(s => s.ComputerId == id && !s.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(s =>
                s.Name.Contains(term) ||
                s.Publisher.Contains(term) ||
                s.Version.Contains(term));
        }

        var items = await query
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Version)
            .Select(s => new ComputerSoftwareItemDto
            {
                Id = s.Id,
                Name = s.Name,
                Version = s.Version,
                Publisher = s.Publisher,
                IsAuthorized = s.IsAuthorized,
                Category = s.Category,
                FirstDetected = s.FirstDetected,
                LastDetected = s.LastDetected,
                InstallLocation = s.InstallLocation
            })
            .ToListAsync();

        return Ok(items);
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
        var tenantId = _tenantAccessor.TenantId;
        var query = _timelineRepo.Query()
            .Where(t => t.ComputerId == id && !t.IsDeleted && t.TenantId == tenantId);
        
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
        var tenantId = _tenantAccessor.TenantId;
        var rawApps = await _appUsageRepo.Query()
            .Where(a => a.ComputerId == id && a.TenantId == tenantId)
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
            IssuedBy = User.Identity?.Name,
            IssuedAt = DateTime.UtcNow
        };
        
        await _eventBus.PublishAsync(new AgentCommandEvent(id, command.Type, command.Payload, User.Identity?.Name));
        return Accepted();
    }

    [HttpPost("{id}/sync-security")]
    [Authorize(Roles = "Admin,SuperAdmin,Operator")]
    public async Task<IActionResult> SyncSecurity(Guid id)
    {
        var tenantId = _tenantAccessor.TenantId;
        var computer = await _computerRepo.Query()
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted);
        if (computer is null) return NotFound();

        var commandId = Guid.NewGuid().ToString();
        var command = new
        {
            CommandId = commandId,
            CommandType = "SyncInventory",
            Parameters = "",
            ReceivedAt = DateTime.UtcNow
        };
        var commandJson = System.Text.Json.JsonSerializer.Serialize(command);

        await _agentHub.Clients.Group($"agent:{id}")
            .SendAsync("ExecuteCommand", commandJson);

        _logger.LogInformation("Security/inventory sync requested for {ComputerId} ({Hostname}) by {User}",
            id, computer.Hostname, User.Identity?.Name);

        return Accepted(new { commandId, computerId = id, message = "Sync requested" });
    }

    [HttpGet("departments")]
    public async Task<ActionResult<List<string>>> GetDepartments()
    {
        var tenantId = _tenantAccessor.TenantId;
        var departments = await _computerRepo.Query()
            .Where(c => !c.IsDeleted && c.TenantId == tenantId && c.Department != null && c.Department != "")
            .Select(c => c.Department!)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync();

        return Ok(departments);
    }

    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> GetStats()
    {
        var tenantId = _tenantAccessor.TenantId;
        var stats = await _cache.GetOrCreateAsync($"dashboard:stats:{tenantId}", async () =>
        {
            var computers = _computerRepo.Query().Where(c => c.TenantId == tenantId);
            return new DashboardStatsDto
            {
                TotalComputers = await computers.CountAsync(),
                OnlineComputers = await computers.CountAsync(c => c.Status == ComputerStatus.Online),
                OfflineComputers = await computers.CountAsync(c => c.Status == ComputerStatus.Offline),
                AwayComputers = await computers.CountAsync(c => c.Status == ComputerStatus.Away),
                DisabledComputers = await computers.CountAsync(c => c.Status == ComputerStatus.Disabled),
                TotalUsers = await computers.Select(c => c.CurrentUser).Distinct().CountAsync(),
                TotalDepartments = await computers.Select(c => c.Department).Distinct().CountAsync(),
                TotalAlerts = await _alertRepo.Query().CountAsync(a => a.Status == AlertStatus.Open && a.TenantId == tenantId)
                    + await ActiveUsbAlertCounter.CountAsync(_timelineRepo.Query(), await computers.Select(c => c.Id).ToListAsync()),
                CriticalAlerts = await _alertRepo.Query().CountAsync(a => a.Status == AlertStatus.Open && a.Severity == Severity.Critical && a.TenantId == tenantId)
            };
        }, TimeSpan.FromSeconds(5));
        
        return Ok(stats);
    }
}
