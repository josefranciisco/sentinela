

namespace Sentinela.Api.Hubs;

[Authorize]
public class RemoteAssistanceHub : Hub
{
    private readonly ILogger<RemoteAssistanceHub> _logger;

    public RemoteAssistanceHub(ILogger<RemoteAssistanceHub> logger)
    {
        _logger = logger;
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
}
