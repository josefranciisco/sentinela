namespace Sentinela.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
[RequirePermission("incidents.view")]
public class AlertsController : ControllerBase
{
    private readonly IRepository<Alert> _alertRepo;
    private readonly IRepository<AlertComment> _commentRepo;
    private readonly ICacheService _cache;
    private readonly IMapper _mapper;
    private readonly ILogger<AlertsController> _logger;
    private readonly ITenantAccessor _tenantAccessor;

    public AlertsController(
        IRepository<Alert> alertRepo,
        IRepository<AlertComment> commentRepo,
        ICacheService cache,
        IMapper mapper,
        ILogger<AlertsController> logger,
        ITenantAccessor tenantAccessor)
    {
        _alertRepo = alertRepo;
        _commentRepo = commentRepo;
        _cache = cache;
        _mapper = mapper;
        _logger = logger;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResult<AlertDto>>> GetAlerts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? severity = null,
        [FromQuery] string? status = null,
        [FromQuery] string? category = null,
        [FromQuery] Guid? computerId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var tenantId = _tenantAccessor.TenantId;
        var query = _alertRepo.Query().Where(a => !a.IsDeleted && a.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(severity) && Enum.TryParse<Severity>(severity, true, out var sev))
            query = query.Where(a => a.Severity == sev);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AlertStatus>(status, true, out var ast))
            query = query.Where(a => a.Status == ast);
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(a => a.Category == category);
        if (computerId.HasValue)
            query = query.Where(a => a.ComputerId == computerId.Value);
        if (from.HasValue)
            query = query.Where(a => a.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(a => a.CreatedAt <= to.Value);

        query = query.OrderByDescending(a => a.CreatedAt);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return Ok(new PaginatedResult<AlertDto>
        {
            Items = _mapper.Map<List<AlertDto>>(items),
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AlertDetailDto>> GetAlert(Guid id)
    {
        var tenantId = _tenantAccessor.TenantId;
        var alert = await _alertRepo.Query()
            .Include(a => a.Comments)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted && a.TenantId == tenantId);

        if (alert is null) return NotFound();
        return Ok(_mapper.Map<AlertDetailDto>(alert));
    }

    [HttpPut("{id}/acknowledge")]
    [Authorize(Roles = "Admin,SuperAdmin,Operator")]
    public async Task<IActionResult> Acknowledge(Guid id)
    {
        var alert = await _alertRepo.GetByIdAsync(id);
        if (alert is null) return NotFound();

        alert.Acknowledge(User.Identity.Name);
        await _alertRepo.UpdateAsync(alert);

        return NoContent();
    }

    [HttpPut("{id}/resolve")]
    [Authorize(Roles = "Admin,SuperAdmin,Operator")]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveAlertDto dto)
    {
        var alert = await _alertRepo.GetByIdAsync(id);
        if (alert is null) return NotFound();

        alert.Resolve(User.Identity.Name);
        await _alertRepo.UpdateAsync(alert);

        if (!string.IsNullOrWhiteSpace(dto.Comment))
        {
            var comment = new AlertComment(id, dto.Comment, User.Identity.Name);
            await _commentRepo.AddAsync(comment);
        }

        return NoContent();
    }

    [HttpPut("{id}/assign")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignAlertDto dto)
    {
        var alert = await _alertRepo.GetByIdAsync(id);
        if (alert is null) return NotFound();

        alert.AssignTo(dto.AssignedTo);
        await _alertRepo.UpdateAsync(alert);

        return NoContent();
    }

    [HttpPost("{id}/comments")]
    public async Task<ActionResult<AlertCommentDto>> AddComment(Guid id, [FromBody] AddCommentDto dto)
    {
        var alert = await _alertRepo.GetByIdAsync(id);
        if (alert is null) return NotFound();

        var comment = new AlertComment(id, dto.Content, User.Identity.Name);
        await _commentRepo.AddAsync(comment);
        return CreatedAtAction(nameof(GetAlert), new { id }, _mapper.Map<AlertCommentDto>(comment));
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "Admin,SuperAdmin,Operator")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateAlertStatusDto dto)
    {
        var alert = await _alertRepo.GetByIdAsync(id);
        if (alert is null) return NotFound();

        if (!Enum.TryParse<AlertStatus>(dto.Status, true, out var newStatus))
            return BadRequest("Invalid status");

        alert.SetStatus(newStatus);
        await _alertRepo.UpdateAsync(alert);

        return NoContent();
    }
}
