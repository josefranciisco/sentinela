using System.Security.Claims;
using Sentinela.Identity.Models;

namespace Sentinela.Identity.Services;

public interface ITokenService
{
    (string accessToken, DateTimeOffset expiresAt) GenerateAccessToken(ApplicationUser user, IList<string> roles);
    string GenerateRefreshToken();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
    Task<bool> RevokeRefreshToken(string refreshToken);
    Task<bool> ValidateRefreshToken(string refreshToken);
}
