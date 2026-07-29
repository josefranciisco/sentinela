namespace Sentinela.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Roles = "Admin,SuperAdmin,Auditor")]
public class AuditController : ControllerBase
{
    private readonly IRepository<AuditTrail> _auditRepo;
    private readonly IMapper _mapper;
    private readonly ILogger<AuditController> _logger;

    public AuditController(
        IRepository<AuditTrail> auditRepo,
        IMapper mapper,
        ILogger<AuditController> logger)
    {
        _auditRepo = auditRepo;
        _mapper = mapper;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResult<AuditLogEntryDto>>> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? userId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? resource = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? sortDirection = "desc")
    {
        var query = _auditRepo.Query();

        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(a => a.UserId == userId);
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action.Contains(action));
        if (!string.IsNullOrWhiteSpace(resource))
            query = query.Where(a => a.Resource == resource);
        if (from.HasValue)
            query = query.Where(a => a.Timestamp >= from.Value);
        if (to.HasValue)
            query = query.Where(a => a.Timestamp <= to.Value);

        query = sortDirection == "asc"
            ? query.OrderBy(a => a.Timestamp)
            : query.OrderByDescending(a => a.Timestamp);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return Ok(new PaginatedResult<AuditLogEntryDto>
        {
            Items = _mapper.Map<List<AuditLogEntryDto>>(items),
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AuditLogEntryDto>> GetAuditLog(Guid id)
    {
        var log = await _auditRepo.GetByIdAsync(id);
        if (log is null) return NotFound();
        return Ok(_mapper.Map<AuditLogEntryDto>(log));
    }
}
