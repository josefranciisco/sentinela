using Sentinela.Identity.Models;

namespace Sentinela.Identity.Services;

public interface ISsoService
{
    Task<LoginResponse> HandleSsoCallbackAsync(string provider, string code, string? deviceInfo, string? ipAddress);
    string GetSsoLoginUrl(string provider);
    bool IsSsoEnabled(string provider);
    Task<SsoUserInfo?> GetUserFromExternalLoginAsync(string provider, string subjectId);
}

public record SsoUserInfo(string SubjectId, string Email, string Username, string DisplayName);
