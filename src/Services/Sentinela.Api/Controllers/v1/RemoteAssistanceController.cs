using Sentinela.Api.Hubs;

namespace Sentinela.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/remote")]
[Authorize]
public class RemoteAssistanceController : ControllerBase
{
    private readonly IRepository<RemoteSession> _sessionRepo;
    private readonly IRepository<Computer> _computerRepo;
    private readonly IMapper _mapper;
    private readonly ILogger<RemoteAssistanceController> _logger;
    private readonly IHubContext<RemoteAssistanceHub> _hubContext;

    public RemoteAssistanceController(
        IRepository<RemoteSession> sessionRepo,
        IRepository<Computer> computerRepo,
        IMapper mapper,
        ILogger<RemoteAssistanceController> logger,
        IHubContext<RemoteAssistanceHub> hubContext)
    {
        _sessionRepo = sessionRepo;
        _computerRepo = computerRepo;
        _mapper = mapper;
        _logger = logger;
        _hubContext = hubContext;
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
            Status = SessionStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };

        await _sessionRepo.AddAsync(session);

        await _hubContext.Clients.Group($"computer:{dto.ComputerId}")
            .SendAsync("SessionRequested", new { session.Id, session.RequestedBy });

        return CreatedAtAction(nameof(GetSessions), new { id = session.Id }, _mapper.Map<RemoteSessionDto>(session));
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<List<RemoteSessionDto>>> GetSessions(
        [FromQuery] string? status = null,
        [FromQuery] Guid? computerId = null)
    {
        var query = _sessionRepo.Query().Where(s => !s.IsDeleted);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SessionStatus>(status, true, out var ss))
            query = query.Where(s => s.Status == ss);
        if (computerId.HasValue)
            query = query.Where(s => s.ComputerId == computerId.Value);

        query = query.OrderByDescending(s => s.RequestedAt);
        var sessions = await query.ToListAsync();

        return Ok(_mapper.Map<List<RemoteSessionDto>>(sessions));
    }

    [HttpPost("sessions/{id}/terminate")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> TerminateSession(Guid id)
    {
        var session = await _sessionRepo.GetByIdAsync(id);
        if (session is null) return NotFound();

        session.Status = SessionStatus.Terminated;
        session.TerminatedAt = DateTime.UtcNow;
        session.TerminatedBy = User.Identity.Name;
        await _sessionRepo.UpdateAsync(session);

        await _hubContext.Clients.Group($"computer:{session.ComputerId}")
            .SendAsync("SessionTerminated", new { session.Id });

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
