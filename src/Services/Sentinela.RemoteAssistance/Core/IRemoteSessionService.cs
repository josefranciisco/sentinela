namespace Sentinela.RemoteAssistance.Core;

public interface IRemoteSessionService
{
    Task<RemoteSession> CreateSessionAsync(Guid computerId, Guid technicianId, string technicianName, SessionMode mode, string? justification);
    Task<RemoteSession?> GetSessionAsync(Guid sessionId);
    Task<IEnumerable<RemoteSession>> GetActiveSessionsAsync();
    Task<IEnumerable<RemoteSession>> GetSessionsByComputerAsync(Guid computerId);
    Task<IEnumerable<RemoteSession>> GetSessionsByTechnicianAsync(Guid technicianId);
    Task<bool> AcceptSessionAsync(Guid sessionId, Guid endUserId, string endUserName);
    Task<bool> RejectSessionAsync(Guid sessionId, string reason);
    Task<bool> EndSessionAsync(Guid sessionId);
    Task<bool> UpdateSessionStatusAsync(Guid sessionId, SessionStatus status);
    Task AddActivityAsync(Guid sessionId, ActivityType type, string description, string performedBy, string? details = null);
    Task<IEnumerable<SessionActivity>> GetSessionActivitiesAsync(Guid sessionId);
    Task<bool> ValidateSessionAsync(Guid sessionId);
    Task CleanupTimedOutSessionsAsync(int timeoutMinutes);
}
