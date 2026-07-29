namespace Sentinela.RemoteAssistance.Core;

public interface ITokenService
{
    string GenerateSessionToken(Guid sessionId, Guid userId, string role, TimeSpan? expiry = null);
    bool ValidateSessionToken(string token, out Guid sessionId, out Guid userId, out string role);
    void RevokeToken(string token);
    bool IsTokenRevoked(string token);
    Task<string> GenerateTemporaryAccessTokenAsync(Guid computerId, Guid technicianId, TimeSpan duration);
}
