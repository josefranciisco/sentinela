using Sentinela.Persistence.Models;

namespace Sentinela.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
[RequirePermission("security.view")]
public class SecurityController : ControllerBase
{
    private readonly IRepository<SecurityEvent> _eventRepo;
    private readonly IRepository<CorrelationRule> _ruleRepo;
    private readonly IRepository<EndpointSecurityStatus> _statusRepo;
    private readonly IRepository<Computer> _computerRepo;
    private readonly ICacheService _cache;
    private readonly IMapper _mapper;
    private readonly ILogger<SecurityController> _logger;
    private readonly ITenantAccessor _tenantAccessor;

    public SecurityController(
        IRepository<SecurityEvent> eventRepo,
        IRepository<CorrelationRule> ruleRepo,
        IRepository<EndpointSecurityStatus> statusRepo,
        IRepository<Computer> computerRepo,
        ICacheService cache,
        IMapper mapper,
        ILogger<SecurityController> logger,
        ITenantAccessor tenantAccessor)
    {
        _eventRepo = eventRepo;
        _ruleRepo = ruleRepo;
        _statusRepo = statusRepo;
        _computerRepo = computerRepo;
        _cache = cache;
        _mapper = mapper;
        _logger = logger;
        _tenantAccessor = tenantAccessor;
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
        var tenantId = _tenantAccessor.TenantId;
        var query = _eventRepo.Query().Where(e => !e.IsDeleted && e.TenantId == tenantId);

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
        var tenantId = _tenantAccessor.TenantId;
        var rules = await _ruleRepo.Query()
            .Where(r => !r.IsDeleted && r.TenantId == tenantId)
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
        var tenantId = _tenantAccessor.TenantId;
        var summary = await _cache.GetOrCreateAsync($"security:summary:{tenantId}", async () =>
        {
            var events = _eventRepo.Query().Where(e => !e.IsDeleted && e.TenantId == tenantId);
            var statuses = _statusRepo.Query().Where(s => !s.IsDeleted && s.TenantId == tenantId);
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
        var tenantId = _tenantAccessor.TenantId;
        var statuses = await _statusRepo.Query()
            .Where(s => !s.IsDeleted && s.TenantId == tenantId)
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

    [HttpGet("incidents")]
    public async Task<ActionResult<List<IncidentDto>>> GetIncidents([FromQuery] int limit = 5)
    {
        var tenantId = _tenantAccessor.TenantId;
        var last24h = DateTimeOffset.UtcNow.AddHours(-24);
        
        var events = await _eventRepo.Query()
            .Where(e => !e.IsDeleted && e.Timestamp >= last24h && e.TenantId == tenantId)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync();

        var computerIds = events.Select(e => e.ComputerId).Distinct().ToList();
        var computers = await _computerRepo.Query()
            .Where(c => computerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Hostname);

        var filteredEvents = events.Where(e => !IsNoiseEvent(e.EventType)).ToList();

        var incidents = filteredEvents
            .GroupBy(e => new { e.ComputerId, Severity = e.Severity })
            .Select(g => new
            {
                ComputerId = g.Key.ComputerId,
                Severity = g.Key.Severity,
                Events = g.ToList()
            })
            .Where(g => g.Events.Any())
            .Select(g => new IncidentDto
            {
                Id = Guid.NewGuid(),
                ComputerId = g.ComputerId,
                ComputerName = computers.GetValueOrDefault(g.ComputerId) ?? "Unknown",
                RiskLevel = GetRiskLevel(g.Severity),
                Title = GenerateIncidentTitle(g.ComputerId, g.Events),
                Description = GenerateIncidentDescription(g.Events),
                Events = g.Events.Select(e => new IncidentEventDto
                {
                    EventType = e.EventType,
                    Description = e.Description,
                    Severity = e.Severity.ToString(),
                    Timestamp = e.Timestamp.UtcDateTime
                }).OrderByDescending(e => e.Timestamp).ToList(),
                Timestamp = g.Events.Max(e => e.Timestamp).UtcDateTime,
                EventCount = g.Events.Count
            })
            .OrderByDescending(i => GetRiskOrder(i.RiskLevel))
            .ThenByDescending(i => i.Timestamp)
            .Take(limit)
            .ToList();

        return Ok(incidents);
    }

    private bool IsNoiseEvent(string eventType)
    {
        var noiseEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AppFocus", "AppStarted", "AppClosed", "IdleStart", "IdleEnd",
            "Login", "Logout", "Lock", "Unlock",
            "AntivirusDisabled", "AntivirusOutdated"
        };
        return noiseEvents.Contains(eventType);
    }

    private string GetRiskLevel(Severity severity)
    {
        return severity switch
        {
            Severity.Critical => "Crítico",
            Severity.High => "Alto",
            Severity.Medium => "Médio",
            Severity.Low => "Baixo",
            _ => "Informativo"
        };
    }

    private int GetRiskOrder(string riskLevel)
    {
        return riskLevel switch
        {
            "Crítico" => 5,
            "Alto" => 4,
            "Médio" => 3,
            "Baixo" => 2,
            _ => 1
        };
    }

    private string GenerateIncidentTitle(Guid computerId, List<SecurityEvent> events)
    {
        var eventTypes = events.Select(e => e.EventType).Distinct().ToList();
        
        if (eventTypes.Contains("CryptominerDetected") || eventTypes.Contains("HighCpuProcess"))
            return "Possível Atividade de Cryptomineração";
        
        if (eventTypes.Contains("MassFileRename"))
            return "Ataque de Ransomware Detectado";
        
        if (eventTypes.Contains("RansomwarePattern"))
            return "Extensão Suspeita de Ransomware";
        
        if (eventTypes.Contains("MalwareDetected"))
            return "Malware Detectado";
        
        if (eventTypes.Contains("AntivirusDisabled"))
            return "Proteção Antivírus Desativada";
        
        if (eventTypes.Contains("FailedLogon"))
            return "Tentativas de Login Falharam";
        
        if (eventTypes.Contains("FileCopy") || eventTypes.Contains("FileTransfer"))
            return "Cópia Crítica de Arquivos para USB";
        
        if (eventTypes.Contains("USBConnected") || eventTypes.Contains("USBDisconnected"))
            return "Atividade de Dispositivo USB";
        
        return $"Múltiplos Eventos de Segurança ({events.Count} eventos)";
    }

    private string GenerateIncidentDescription(List<SecurityEvent> events)
    {
        var descriptions = new List<string>();
        
        foreach (var group in events.GroupBy(e => e.EventType).OrderByDescending(g => g.Max(e => e.Severity)))
        {
            descriptions.Add($"{group.Key}: {group.Count()} ocorrência(s)");
        }
        
        return string.Join("\n", descriptions);
    }
}
