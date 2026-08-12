using Sentinela.Api.Hubs;

namespace Sentinela.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/remote")]
[Authorize]
[RequirePermission("remote.view")]
public class RemoteAssistanceController : ControllerBase
{
    private readonly IRepository<RemoteSession> _sessionRepo;
    private readonly IRepository<Computer> _computerRepo;
    private readonly IMapper _mapper;
    private readonly ILogger<RemoteAssistanceController> _logger;
    private readonly IHubContext<RemoteAssistanceHub> _hubContext;
    private readonly IHubContext<AgentHub> _agentHubContext;

    public RemoteAssistanceController(
        IRepository<RemoteSession> sessionRepo,
        IRepository<Computer> computerRepo,
        IMapper mapper,
        ILogger<RemoteAssistanceController> logger,
        IHubContext<RemoteAssistanceHub> hubContext,
        IHubContext<AgentHub> agentHubContext)
    {
        _sessionRepo = sessionRepo;
        _computerRepo = computerRepo;
        _mapper = mapper;
        _logger = logger;
        _hubContext = hubContext;
        _agentHubContext = agentHubContext;
    }

    [HttpPost("request")]
    [Authorize(Roles = "Admin,SuperAdmin,Operator")]
    public async Task<ActionResult<RemoteSessionDto>> RequestSession([FromBody] RequestSessionDto dto)
    {
        var computer = await _computerRepo.GetByIdAsync(dto.ComputerId);
        if (computer is null) return NotFound("Computer not found");

        var session = new RemoteSession
        {
            ComputerId = dto.ComputerId,
            RequestedBy = User.Identity.Name,
            SessionType = dto.SessionType,
            Status = "Active",
            RequestedAt = DateTime.UtcNow,
            MonitorIndex = dto.MonitorIndex
        };

        await _sessionRepo.AddAsync(session);

        await _hubContext.Clients.Group($"computer:{dto.ComputerId}")
            .SendAsync("SessionRequested", new { session.Id, session.RequestedBy });

        await _agentHubContext.Clients.Group($"agent:{dto.ComputerId}")
            .SendAsync("StartRemoteSession", new
            {
                SessionId = session.Id,
                SessionType = session.SessionType,
                MonitorIndex = session.MonitorIndex
            });

        _logger.LogInformation("Remote session {SessionId} requested for computer {ComputerId} by {User}",
            session.Id, dto.ComputerId, User.Identity.Name);

        return CreatedAtAction(nameof(GetSessions), new { id = session.Id }, _mapper.Map<RemoteSessionDto>(session));
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<List<RemoteSessionDto>>> GetSessions(
        [FromQuery] string? status = null,
        [FromQuery] Guid? computerId = null)
    {
        var query = _sessionRepo.Query().Where(s => !s.IsDeleted);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(s => s.Status == status);
        if (computerId.HasValue)
            query = query.Where(s => s.ComputerId == computerId.Value);

        query = query.OrderByDescending(s => s.RequestedAt);
        var sessions = await query.ToListAsync();

        var dto = _mapper.Map<List<RemoteSessionDto>>(sessions);
        foreach (var item in dto)
        {
            var comp = await _computerRepo.GetByIdAsync(item.ComputerId);
            if (comp != null) item.ComputerName = comp.Hostname;
        }

        return Ok(dto);
    }

    [HttpPost("sessions/{id}/terminate")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> TerminateSession(Guid id)
    {
        var session = await _sessionRepo.GetByIdAsync(id);
        if (session is null) return NotFound();

        session.Status = "Terminated";
        session.TerminatedAt = DateTime.UtcNow;
        session.TerminatedBy = User.Identity.Name;
        await _sessionRepo.UpdateAsync(session);

        await _hubContext.Clients.Group($"computer:{session.ComputerId}")
            .SendAsync("SessionTerminated", new { session.Id });

        await _agentHubContext.Clients.Group($"agent:{session.ComputerId}")
            .SendAsync("StopRemoteSession", new { SessionId = session.Id });

        return NoContent();
    }

    [HttpDelete("sessions/{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> DeleteSession(Guid id)
    {
        var session = await _sessionRepo.GetByIdAsync(id);
        if (session is null) return NotFound();

        if (session.Status == "Active")
        {
            await _agentHubContext.Clients.Group($"agent:{session.ComputerId}")
                .SendAsync("StopRemoteSession", new { SessionId = session.Id });
        }

        await _sessionRepo.DeleteAsync(session);

        return NoContent();
    }

    [HttpPost("{computerId}/command")]
    [Authorize(Roles = "Admin,SuperAdmin,Operator")]
    public async Task<IActionResult> SendRemoteCommand(Guid computerId, [FromBody] RemoteCommandDto dto)
    {
        var computer = await _computerRepo.GetByIdAsync(computerId);
        if (computer is null) return NotFound("Computer not found");

        await _hubContext.Clients.Group($"computer:{computerId}")
            .SendAsync("RemoteCommand", new
            {
                Command = dto.Command,
                Parameters = dto.Parameters,
                IssuedBy = User.Identity.Name
            });

        _logger.LogInformation("Remote command {Command} sent to computer {ComputerId} by {User}",
            dto.Command, computerId, User.Identity.Name);

        return Accepted();
    }
}
