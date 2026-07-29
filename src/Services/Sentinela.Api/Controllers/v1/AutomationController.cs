namespace Sentinela.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/automation")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AutomationController : ControllerBase
{
    private readonly IRepository<Workflow> _workflowRepo;
    private readonly IMapper _mapper;
    private readonly ILogger<AutomationController> _logger;

    public AutomationController(
        IRepository<Workflow> workflowRepo,
        IMapper mapper,
        ILogger<AutomationController> logger)
    {
        _workflowRepo = workflowRepo;
        _mapper = mapper;
        _logger = logger;
    }

    [HttpGet("workflows")]
    public async Task<ActionResult<PaginatedResult<WorkflowDto>>> GetWorkflows(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? enabled = null)
    {
        var query = _workflowRepo.Query().Where(w => !w.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(w => w.Name.Contains(search) || w.Description.Contains(search));
        if (enabled.HasValue)
            query = query.Where(w => w.IsEnabled == enabled.Value);

        query = query.OrderByDescending(w => w.CreatedAt);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return Ok(new PaginatedResult<WorkflowDto>
        {
            Items = _mapper.Map<List<WorkflowDto>>(items),
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }

    [HttpGet("workflows/{id}")]
    public async Task<ActionResult<WorkflowDto>> GetWorkflow(Guid id)
    {
        var workflow = await _workflowRepo.GetByIdAsync(id);
        if (workflow is null) return NotFound();
        return Ok(_mapper.Map<WorkflowDto>(workflow));
    }

    [HttpPost("workflows")]
    public async Task<ActionResult<WorkflowDto>> CreateWorkflow([FromBody] CreateWorkflowDto dto)
    {
        var workflow = new Workflow(
            dto.Name,
            dto.Description,
            dto.Trigger);

        foreach (var c in dto.Conditions)
        {
            var op = Enum.Parse<WorkflowCondition.ComparisonOperator>(c.Operator);
            workflow.AddCondition(c.Field, op, c.Value);
        }

        foreach (var a in dto.Actions)
        {
            var actionType = Enum.Parse<ActionType>(a.Type);
            workflow.AddAction(actionType, a.Parameters, a.Order);
        }

        await _workflowRepo.AddAsync(workflow);
        return CreatedAtAction(nameof(GetWorkflow), new { id = workflow.Id }, _mapper.Map<WorkflowDto>(workflow));
    }

    [HttpPut("workflows/{id}")]
    public async Task<IActionResult> UpdateWorkflow(Guid id, [FromBody] UpdateWorkflowDto dto)
    {
        var workflow = await _workflowRepo.GetByIdAsync(id);
        if (workflow is null) return NotFound();

        workflow.UpdateDetails(dto.Name, dto.Description, dto.Trigger);
        workflow.MarkAsUpdated();

        await _workflowRepo.UpdateAsync(workflow);
        return NoContent();
    }

    [HttpDelete("workflows/{id}")]
    public async Task<IActionResult> DeleteWorkflow(Guid id)
    {
        var workflow = await _workflowRepo.GetByIdAsync(id);
        if (workflow is null) return NotFound();

        workflow.MarkAsDeleted();
        await _workflowRepo.UpdateAsync(workflow);

        return NoContent();
    }

    [HttpPost("workflows/{id}/toggle")]
    public async Task<IActionResult> ToggleWorkflow(Guid id)
    {
        var workflow = await _workflowRepo.GetByIdAsync(id);
        if (workflow is null) return NotFound();

        if (workflow.IsEnabled)
            workflow.Disable();
        else
            workflow.Enable();

        workflow.MarkAsUpdated();
        await _workflowRepo.UpdateAsync(workflow);

        return Ok(new { enabled = workflow.IsEnabled });
    }
}
