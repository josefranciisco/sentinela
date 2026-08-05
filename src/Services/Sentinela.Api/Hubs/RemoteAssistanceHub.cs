

namespace Sentinela.Api.Hubs;

[Authorize]
public class RemoteAssistanceHub : Hub
{
    private readonly ILogger<RemoteAssistanceHub> _logger;
    private readonly IRepository<RemoteSession> _sessionRepo;
    private readonly IHubContext<AgentHub> _agentHubContext;

    public RemoteAssistanceHub(
        ILogger<RemoteAssistanceHub> logger,
        IRepository<RemoteSession> sessionRepo,
        IHubContext<AgentHub> agentHubContext)
    {
        _logger = logger;
        _sessionRepo = sessionRepo;
        _agentHubContext = agentHubContext;
    }

    public override async Task OnConnectedAsync()
    {
        var sessionId = Context.GetHttpContext()?.Request.Query["sessionId"].FirstOrDefault();
        if (sessionId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"session:{sessionId}");
            _logger.LogInformation("User joined remote session {SessionId}", sessionId);
        }
        await base.OnConnectedAsync();
    }

    public async Task OfferScreenShare(string sessionId, string connectionId)
    {
        await Clients.Group($"session:{sessionId}").SendAsync("ScreenShareOffered", new
        {
            ConnectionId = Context.ConnectionId,
            User = Context.User?.Identity?.Name
        });
    }

    public async Task RequestControl(string sessionId)
    {
        await Clients.Group($"session:{sessionId}").SendAsync("ControlRequested", new
        {
            ConnectionId = Context.ConnectionId,
            User = Context.User?.Identity?.Name
        });
    }

    public async Task AcceptControl(string sessionId, string targetConnectionId)
    {
        await Clients.Client(targetConnectionId).SendAsync("ControlAccepted", new
        {
            ConnectionId = Context.ConnectionId,
            User = Context.User?.Identity?.Name
        });
    }

    public async Task SendInput(string sessionId, InputEventDto input)
    {
        await Clients.OthersInGroup($"session:{sessionId}").SendAsync("InputReceived", input);
    }

    public async Task ChatMessage(string sessionId, string message)
    {
        await Clients.Group($"session:{sessionId}").SendAsync("ChatMessageReceived", new
        {
            ConnectionId = Context.ConnectionId,
            User = Context.User?.Identity?.Name,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task FileTransferChunk(string sessionId, FileChunkDto chunk)
    {
        await Clients.OthersInGroup($"session:{sessionId}").SendAsync("FileChunkReceived", chunk);
    }

    public async Task EndSession(string sessionId)
    {
        await Clients.Group($"session:{sessionId}").SendAsync("SessionEnded", new
        {
            EndedBy = Context.User?.Identity?.Name
        });
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"session:{sessionId}");
    }

    public async Task SwitchMonitor(string sessionId, int? monitorIndex)
    {
        if (!Guid.TryParse(sessionId, out var sessionGuid))
        {
            _logger.LogWarning("Invalid session id in SwitchMonitor: {SessionId}", sessionId);
            return;
        }

        var session = await _sessionRepo.GetByIdAsync(sessionGuid);
        if (session is null || session.Status != "Active")
        {
            _logger.LogWarning("Session {SessionId} not found or not active for SwitchMonitor", sessionId);
            return;
        }

        session.MonitorIndex = monitorIndex;
        await _sessionRepo.UpdateAsync(session);

        await _agentHubContext.Clients.Group($"agent:{session.ComputerId}")
            .SendAsync("SwitchRemoteSessionMonitor", new
            {
                SessionId = sessionId,
                MonitorIndex = monitorIndex
            });

        _logger.LogInformation("Monitor switch requested for session {SessionId} to index {MonitorIndex}",
            sessionId, monitorIndex);
    }
}
