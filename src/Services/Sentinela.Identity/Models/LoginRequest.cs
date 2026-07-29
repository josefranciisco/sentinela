namespace Sentinela.Identity.Models;

public record LoginRequest(string Username, string? Email, string Password, string? TwoFactorCode, string? RememberMe);

public record RegisterRequest(string Username, string Email, string Password, string? Department);

public record RefreshTokenRequest(string AccessToken, string RefreshToken);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmNewPassword);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Email, string Token, string NewPassword, string ConfirmPassword);

public record TwoFactorSetupResponse(string SharedKey, string QrCodeUri, string[] RecoveryCodes);

public record TwoFactorVerifyRequest(string Code);

public record TwoFactorRecoveryRequest(string RecoveryCode);

public record LoginResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, UserInfo User);

public record UserInfo(Guid Id, string Username, string Email, string[] Roles, bool TwoFactorEnabled);

public record AuthErrorResponse(string Code, string Message, string? Details);

public record LdapConfig(string Server, int Port, bool UseSsl, string BaseDn, string AdminBindDn, string AdminPassword, string UserSearchBase, string GroupSearchBase);

public record SsoConfig(string Authority, string ClientId, string ClientSecret, string CallbackPath, string[] Scopes);

public record JwtConfig(string Secret, string Issuer, string Audience, int AccessTokenExpirationMinutes, int RefreshTokenExpirationDays);

public record RateLimitConfig(int MaxRequestsPerMinute, int MaxLoginAttempts, int LockoutMinutes);
