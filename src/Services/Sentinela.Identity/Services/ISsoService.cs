using Sentinela.Identity.Models;

namespace Sentinela.Identity.Services;

public interface ISsoService
{
    string BuildLoginUrl(string provider, string state);
    Task<LoginResponse> HandleSsoCallbackAsync(string provider, string code, string? deviceInfo, string? ipAddress);
    bool IsSsoEnabled(string provider);
}

public record SsoUserInfo(string SubjectId, string Email, string Username, string DisplayName);