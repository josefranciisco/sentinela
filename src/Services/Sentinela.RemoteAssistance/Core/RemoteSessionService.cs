using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sentinela.RemoteAssistance.Configuration;

namespace Sentinela.RemoteAssistance.Core;

public class RemoteSessionService : IRemoteSessionService
{
    private readonly ConcurrentDictionary<Guid, RemoteSession> _sessions = new();
    private readonly ConcurrentDictionary<Guid, List<SessionActivity>> _activities = new();
    private readonly ILogger<RemoteSessionService> _logger;
    private readonly RemoteAssistanceOptions _options;

    public RemoteSessionService(IOptions<RemoteAssistanceOptions> options, ILogger<RemoteSessionService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<RemoteSession> CreateSessionAsync(Guid computerId, Guid technicianId, string technicianName, SessionMode mode, string? justification)
    {
        if (!_options.Enabled)
            throw new InvalidOperationException("Remote assistance is disabled.");

        if (_sessions.Values.Count(s => s.Status is SessionStatus.Connecting or SessionStatus.Connected) >= _options.MaxConcurrentSessions)
            throw new InvalidOperationException($"Maximum concurrent sessions reached ({_options.MaxConcurrentSessions}).");

        if (_options.RequireJustification && string.IsNullOrWhiteSpace(justification))
            throw new ArgumentException("Justification is required for remote sessions.", nameof(justification));

        var session = new RemoteSession
        {
            Id = Guid.NewGuid(),
            ComputerId = computerId,
            TechnicianId = technicianId,
            TechnicianName = technicianName,
            Mode = mode,
            Justification = justification,
            Status = SessionStatus.Requested,
            RequestedAt = DateTimeOffset.UtcNow,
            IsAudited = _options.FullAuditEnabled
        };

        _sessions.TryAdd(session.Id, session);
        _logger.LogInformation("Session created: {SessionId} for computer {ComputerId}", session.Id, computerId);

        return Task.FromResult(session);
    }

    public Task<RemoteSession?> GetSessionAsync(Guid sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public Task<IEnumerable<RemoteSession>> GetActiveSessionsAsync()
    {
        var active = _sessions.Values.Where(s => s.Status is SessionStatus.Connecting or SessionStatus.Connected);
        return Task.FromResult(active);
    }

    public Task<IEnumerable<RemoteSession>> GetSessionsByComputerAsync(Guid computerId)
    {
        var result = _sessions.Values.Where(s => s.ComputerId == computerId);
        return Task.FromResult(result);
    }

    public Task<IEnumerable<RemoteSession>> GetSessionsByTechnicianAsync(Guid technicianId)
    {
        var result = _sessions.Values.Where(s => s.TechnicianId == technicianId);
        return Task.FromResult(result);
    }

    public Task<bool> AcceptSessionAsync(Guid sessionId, Guid endUserId, string endUserName)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return Task.FromResult(false);

        if (session.Status != SessionStatus.Requested)
            return Task.FromResult(false);

        session.Status = SessionStatus.Connected;
        session.EndUserId = endUserId;
        session.EndUserName = endUserName;
        session.StartedAt = DateTimeOffset.UtcNow;

        AddActivityInternal(sessionId, ActivityType.SessionStarted, $"Session accepted by {endUserName}", endUserName);
        _logger.LogInformation("Session {SessionId} accepted by {EndUser}", sessionId, endUserName);

        return Task.FromResult(true);
    }

    public Task<bool> RejectSessionAsync(Guid sessionId, string reason)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return Task.FromResult(false);

        session.Status = SessionStatus.Rejected;
        session.EndedAt = DateTimeOffset.UtcNow;

        AddActivityInternal(sessionId, ActivityType.SessionEnded, $"Session rejected: {reason}", "System");
        _logger.LogWarning("Session {SessionId} rejected: {Reason}", sessionId, reason);

        return Task.FromResult(true);
    }

    public Task<bool> EndSessionAsync(Guid sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return Task.FromResult(false);

        session.Status = SessionStatus.Disconnected;
        session.EndedAt = DateTimeOffset.UtcNow;

        AddActivityInternal(sessionId, ActivityType.SessionEnded, "Session ended", "System");
        _logger.LogInformation("Session {SessionId} ended", sessionId);

        return Task.FromResult(true);
    }

    public Task<bool> UpdateSessionStatusAsync(Guid sessionId, SessionStatus status)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return Task.FromResult(false);

        session.Status = status;

        if (status is SessionStatus.Disconnected or SessionStatus.Failed or SessionStatus.TimedOut)
            session.EndedAt = DateTimeOffset.UtcNow;

        return Task.FromResult(true);
    }

    public Task AddActivityAsync(Guid sessionId, ActivityType type, string description, string performedBy, string? details = null)
    {
        AddActivityInternal(sessionId, type, description, performedBy, details);
        return Task.CompletedTask;
    }

    private void AddActivityInternal(Guid sessionId, ActivityType type, string description, string performedBy, string? details = null)
    {
        var activity = new SessionActivity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Type = type,
            Description = description,
            PerformedBy = performedBy,
            Timestamp = DateTimeOffset.UtcNow,
            Details = details
        };

        var activities = _activities.GetOrAdd(sessionId, _ => new List<SessionActivity>());
        lock (activities)
        {
            activities.Add(activity);
        }

        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.Activities.Add(activity);
        }
    }

    public Task<IEnumerable<SessionActivity>> GetSessionActivitiesAsync(Guid sessionId)
    {
        if (_activities.TryGetValue(sessionId, out var activities))
        {
            lock (activities)
            {
                return Task.FromResult(activities.AsEnumerable());
            }
        }

        return Task.FromResult(Enumerable.Empty<SessionActivity>());
    }

    public Task<bool> ValidateSessionAsync(Guid sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return Task.FromResult(false);

        var isValid = session.Status is SessionStatus.Connecting or SessionStatus.Connected;

        if (isValid && session.StartedAt.HasValue)
        {
            var elapsed = DateTimeOffset.UtcNow - session.StartedAt.Value;
            if (elapsed.TotalMinutes > _options.SessionTimeoutMinutes)
            {
                session.Status = SessionStatus.TimedOut;
                session.EndedAt = DateTimeOffset.UtcNow;
                _logger.LogWarning("Session {SessionId} timed out after {Minutes} minutes", sessionId, _options.SessionTimeoutMinutes);
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(isValid);
    }

    public Task CleanupTimedOutSessionsAsync(int timeoutMinutes)
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-timeoutMinutes);
        var timedOut = _sessions.Values
            .Where(s => s.Status is SessionStatus.Connecting or SessionStatus.Connected && s.StartedAt < cutoff)
            .ToList();

        foreach (var session in timedOut)
        {
            session.Status = SessionStatus.TimedOut;
            session.EndedAt = DateTimeOffset.UtcNow;
            AddActivityInternal(session.Id, ActivityType.SessionEnded, "Session timed out", "System");
            _logger.LogInformation("Session {SessionId} automatically timed out", session.Id);
        }

        return Task.CompletedTask;
    }
}
