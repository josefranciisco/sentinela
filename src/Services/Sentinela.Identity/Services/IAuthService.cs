using Sentinela.Identity.Models;

namespace Sentinela.Identity.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, string? deviceInfo, string? ipAddress);
    Task<LoginResponse> RegisterAsync(RegisterRequest request);
    Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request);
    Task LogoutAsync(string refreshToken);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    Task ForgotPasswordAsync(ForgotPasswordRequest request);
    Task ResetPasswordAsync(ResetPasswordRequest request);
    Task<UserInfo> GetUserInfoAsync(Guid userId);
    Task<TwoFactorSetupResponse> SetupTwoFactorAsync(Guid userId);
    Task<bool> VerifyTwoFactorAsync(Guid userId, string code);
    Task<bool> UseRecoveryCodeAsync(Guid userId, string recoveryCode);
    Task<bool> ValidateSessionAsync(string accessToken);
}
