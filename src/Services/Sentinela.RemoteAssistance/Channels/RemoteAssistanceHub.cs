using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Sentinela.RemoteAssistance.Core;

namespace Sentinela.RemoteAssistance.Channels;

[Authorize]
public class RemoteAssistanceHub : Hub
{
    private readonly ILogger<RemoteAssistanceHub> _logger;
    private static readonly ConcurrentDictionary<string, RemoteSession> ActiveSessions = new();

    public RemoteAssistanceHub(ILogger<RemoteAssistanceHub> logger)
    {
        _logger = logger;
    }

    public async Task RequestSession(Guid computerId, SessionMode mode, string justification)
    {
        var session = new RemoteSession
        {
            Id = Guid.NewGuid(),
            ComputerId = computerId,
            TechnicianId = GetUserId(),
            TechnicianName = GetUserName(),
            Mode = mode,
            Justification = justification,
            RequestedAt = DateTimeOffset.UtcNow,
            ConnectionId = Context.ConnectionId
        };

        ActiveSessions.TryAdd(session.Id.ToString(), session);

        await Clients.Group($"computer:{computerId}").SendAsync("SessionRequested", session);

        _logger.LogInformation("Remote session requested: {SessionId} by {Technician} on {Computer}",
            session.Id, session.TechnicianName, computerId);
    }

    public async Task AcceptSession(Guid sessionId)
    {
        if (ActiveSessions.TryGetValue(sessionId.ToString(), out var session))
        {
            session.Status = SessionStatus.Connected;
            session.StartedAt = DateTimeOffset.UtcNow;

            await Groups.AddToGroupAsync(Context.ConnectionId, $"session:{sessionId}");
            await Clients.Group($"session:{sessionId}").SendAsync("SessionAccepted", session);
        }
    }

    public async Task SendScreenFrame(Guid sessionId, byte[] frameData, int quality)
    {
        if (ActiveSessions.TryGetValue(sessionId.ToString(), out var session))
        {
            await Clients.Client(session.ConnectionId).SendAsync("ScreenFrame", frameData, quality);
        }
    }

    public async Task SendChatMessage(Guid sessionId, string message)
    {
        var activity = new SessionActivity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Type = ActivityType.ChatMessage,
            Description = message,
            PerformedBy = GetUserName(),
            Timestamp = DateTimeOffset.UtcNow
        };

        await Clients.Group($"session:{sessionId}").SendAsync("ChatMessage", activity);
    }

    public async Task ExecuteRemoteCommand(Guid computerId, CommandType commandType, string? parameters)
    {
        var command = new RemoteCommand
        {
            Id = Guid.NewGuid(),
            ComputerId = computerId,
            Type = commandType,
            Parameters = parameters,
            IssuedBy = GetUserName(),
            IssuedAt = DateTimeOffset.UtcNow
        };

        await Clients.Group($"computer:{computerId}").SendAsync("ExecuteCommand", command);
    }

    public async Task SendFileChunk(Guid sessionId, Guid transferId, byte[] chunk, int chunkIndex, int totalChunks)
    {
        await Clients.Group($"session:{sessionId}").SendAsync("FileChunk", transferId, chunk, chunkIndex, totalChunks);
    }

    public async Task EndSession(Guid sessionId)
    {
        if (ActiveSessions.TryRemove(sessionId.ToString(), out var session))
        {
            session.Status = SessionStatus.Disconnected;
            session.EndedAt = DateTimeOffset.UtcNow;

            await Clients.Group($"session:{sessionId}").SendAsync("SessionEnded", sessionId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"session:{sessionId}");

            _logger.LogInformation("Remote session ended: {SessionId} (Duration: {Duration})",
                sessionId, session.EndedAt - session.StartedAt);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var (key, session) in ActiveSessions)
        {
            if (session.ConnectionId == Context.ConnectionId ||
                session.ComputerId.ToString() == Context.UserIdentifier)
            {
                await EndSession(session.Id);
            }
        }
        await base.OnDisconnectedAsync(exception);
    }

    private Guid GetUserId() => Guid.Parse(Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
    private string GetUserName() => Context.User?.Identity?.Name ?? "Unknown";
}
