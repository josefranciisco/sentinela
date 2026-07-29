using Sentinela.Persistence.Models;

namespace Sentinela.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class SecurityController : ControllerBase
{
    private readonly IRepository<SecurityEvent> _eventRepo;
    private readonly IRepository<CorrelationRule> _ruleRepo;
    private readonly IRepository<EndpointSecurityStatus> _statusRepo;
    private readonly IRepository<Computer> _computerRepo;
    private readonly ICacheService _cache;
    private readonly IMapper _mapper;
    private readonly ILogger<SecurityController> _logger;

    public SecurityController(
        IRepository<SecurityEvent> eventRepo,
        IRepository<CorrelationRule> ruleRepo,
        IRepository<EndpointSecurityStatus> statusRepo,
        IRepository<Computer> computerRepo,
        ICacheService cache,
        IMapper mapper,
        ILogger<SecurityController> logger)
    {
        _eventRepo = eventRepo;
        _ruleRepo = ruleRepo;
        _statusRepo = statusRepo;
        _computerRepo = computerRepo;
        _cache = cache;
        _mapper = mapper;
        _logger = logger;
    }

    [HttpGet("events")]
    public async Task<ActionResult<PaginatedResult<SecurityEventDto>>> GetSecurityEvents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? category = null,
        [FromQuery] string? severity = null,
        [FromQuery] string? sourceIp = null,
        [FromQuery] Guid? computerId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var query = _eventRepo.Query().Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(e => e.Category == category);
        if (!string.IsNullOrWhiteSpace(severity) && Enum.TryParse<Severity>(severity, true, out var sev))
            query = query.Where(e => e.Severity == sev);
        if (!string.IsNullOrWhiteSpace(sourceIp))
            query = query.Where(e => e.SourceIp == sourceIp);
        if (computerId.HasValue)
            query = query.Where(e => e.ComputerId == computerId.Value);
        if (from.HasValue)
            query = query.Where(e => e.Timestamp >= from.Value);
        if (to.HasValue)
            query = query.Where(e => e.Timestamp <= to.Value);

        query = query.OrderByDescending(e => e.Timestamp);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var computerIds = items.Select(i => i.ComputerId).Distinct().ToList();
        var computers = await _computerRepo.Query()
            .Where(c => computerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Hostname);

        var dtos = items.Select(e => new SecurityEventDto
        {
            Id = e.Id,
            ComputerId = e.ComputerId,
            EventType = e.EventType,
            Category = e.Category,
            Severity = e.Severity.ToString(),
            Description = e.Description,
            Username = e.Username,
            SourceIp = e.SourceIp,
            Timestamp = e.Timestamp.UtcDateTime,
            ComputerName = computers.GetValueOrDefault(e.ComputerId)
        }).ToList();

        return Ok(new PaginatedResult<SecurityEventDto>
        {
            Items = dtos,
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }

    [HttpGet("correlations")]
    public async Task<ActionResult<List<CorrelationRuleDto>>> GetCorrelationRules()
    {
        var rules = await _ruleRepo.Query()
            .Where(r => !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return Ok(_mapper.Map<List<CorrelationRuleDto>>(rules));
    }

    [HttpPost("correlations")]
    public async Task<ActionResult<CorrelationRuleDto>> CreateCorrelationRule([FromBody] CreateCorrelationRuleDto dto)
    {
        var rule = new CorrelationRule(
            dto.Name,
            dto.EventPattern,
            TimeSpan.FromMinutes(dto.TimeWindow),
            description: dto.Description);

        await _ruleRepo.AddAsync(rule);
        return CreatedAtAction(nameof(GetCorrelationRules), null, _mapper.Map<CorrelationRuleDto>(rule));
    }

    [HttpGet("summary")]
    public async Task<ActionResult<SecuritySummaryDto>> GetSecuritySummary()
    {
        var summary = await _cache.GetOrCreateAsync("security:summary", async () =>
        {
            var events = _eventRepo.Query().Where(e => !e.IsDeleted);
            var statuses = _statusRepo.Query().Where(s => !s.IsDeleted);
            var now = DateTimeOffset.UtcNow;
            var last24h = now.AddHours(-24);
            var last7d = now.AddDays(-7);

            var statusList = await statuses.ToListAsync();
            var totalEndpoints = Math.Max(statusList.Count, 1);

            var compliance = new List<SecurityComplianceDto>
            {
                new() { Name = "Firewall", Value = (int)(100.0 * statusList.Count(s => s.FirewallEnabled) / totalEndpoints) },
                new() { Name = "Defender", Value = (int)(100.0 * statusList.Count(s => s.DefenderEnabled || s.RealTimeProtectionEnabled) / totalEndpoints) },
                new() { Name = "BitLocker", Value = (int)(100.0 * statusList.Count(s => s.BitlockerEnabled) / totalEndpoints) },
                new() { Name = "RDP Hardened", Value = (int)(100.0 * statusList.Count(s => !s.RdpEnabled) / totalEndpoints) },
            };

            var atRisk = statusList.Count(s =>
                !s.AntivirusEnabled
                || !s.RealTimeProtectionEnabled
                || s.AntivirusSignatureAgeDays > 7);

            return new SecuritySummaryDto
            {
                EventsLast24h = await events.CountAsync(e => e.Timestamp >= last24h),
                EventsLast7d = await events.CountAsync(e => e.Timestamp >= last7d),
                CriticalEvents = await events.CountAsync(e => e.Severity == Severity.Critical && e.Timestamp >= last24h),
                HighEvents = await events.CountAsync(e => e.Severity == Severity.High && e.Timestamp >= last24h),
                OpenIncidents = await events.CountAsync(e => !e.IsResolved && e.Severity >= Severity.Medium),
                ComputersAtRisk = atRisk,
                ActiveCorrelationRules = await _ruleRepo.Query().CountAsync(r => r.IsEnabled && !r.IsDeleted),
                TopThreatCategories = await events
                    .Where(e => e.Timestamp >= last7d)
                    .GroupBy(e => e.Category)
                    .Select(g => new ThreatCategoryDto
                    {
                        Category = g.Key,
                        Count = g.Count()
                    })
                    .OrderByDescending(t => t.Count)
                    .Take(10)
                    .ToListAsync(),
                Compliance = compliance
            };
        }, TimeSpan.FromSeconds(30));

        return Ok(summary);
    }

    [HttpGet("compliance")]
    public async Task<ActionResult<List<EndpointSecurityStatusDto>>> GetEndpointCompliance()
    {
        var statuses = await _statusRepo.Query()
            .Where(s => !s.IsDeleted)
            .OrderByDescending(s => s.CollectedAt)
            .ToListAsync();

        return Ok(statuses.Select(s => new EndpointSecurityStatusDto
        {
            ComputerId = s.ComputerId,
            FirewallEnabled = s.FirewallEnabled,
            DefenderEnabled = s.DefenderEnabled,
            AntivirusEnabled = s.AntivirusEnabled,
            RealTimeProtectionEnabled = s.RealTimeProtectionEnabled,
            AntivirusSignatureAgeDays = s.AntivirusSignatureAgeDays,
            AntivirusSignatureLastUpdated = s.AntivirusSignatureLastUpdated?.UtcDateTime,
            AntivirusProductName = s.AntivirusProductName,
            BitlockerEnabled = s.BitlockerEnabled,
            RdpEnabled = s.RdpEnabled,
            CollectedAt = s.CollectedAt.UtcDateTime
        }).ToList());
    }
}
