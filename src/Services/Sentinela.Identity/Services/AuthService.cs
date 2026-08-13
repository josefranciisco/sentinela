using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Sentinela.Identity.Configuration;
using Sentinela.Identity.Models;
using Sentinela.Identity.Stores;
using Sentinela.Persistence;

namespace Sentinela.Identity.Services;

public class AuthService : IAuthService
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly ITwoFactorService _twoFactorService;
    private readonly IdentityDbContext _context;
    private readonly SentinelaDbContext _sentinelaContext;
    private readonly ILogger<AuthService> _logger;
    private ILdapService? _ldapService;
    private readonly IServiceProvider _serviceProvider;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        ITwoFactorService twoFactorService,
        IdentityDbContext context,
        SentinelaDbContext sentinelaContext,
        ILogger<AuthService> logger,
        IServiceProvider serviceProvider)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _twoFactorService = twoFactorService;
        _context = context;
        _sentinelaContext = sentinelaContext;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    private ILdapService? LdapService =>
        _ldapService ??= _serviceProvider.GetService<ILdapService>();

    public async Task<LoginResponse> LoginAsync(LoginRequest request, string? deviceInfo, string? ipAddress)
    {
        var user = await _userManager.FindByNameAsync(request.Username)
                   ?? await _userManager.FindByEmailAsync(request.Email);

        if (user is null || !user.IsActive)
        {
            _logger.LogWarning("Login failed for user {Username}: user not found or inactive", request.Username);
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
        {
            var remaining = user.LockoutEnd.Value - DateTimeOffset.UtcNow;
            _logger.LogWarning("User {Username} is locked out for {Minutes} more minutes", request.Username, remaining.TotalMinutes);
            throw new UnauthorizedAccessException($"Account is locked. Try again in {remaining.Minutes} minutes.");
        }

        if (user.TwoFactorEnabled && string.IsNullOrEmpty(request.TwoFactorCode))
        {
            throw new InvalidOperationException("Two-factor authentication code is required.");
        }

        if (user.TwoFactorEnabled && !string.IsNullOrEmpty(request.TwoFactorCode))
        {
            var isValid = _twoFactorService.VerifyCode(user.TwoFactorSecret ?? string.Empty, request.TwoFactorCode);
            if (!isValid)
            {
                _logger.LogWarning("2FA verification failed for user {Username}", request.Username);
                throw new UnauthorizedAccessException("Invalid two-factor authentication code.");
            }
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, true);

        if (result.IsLockedOut)
        {
            _logger.LogWarning("User {Username} is locked out", request.Username);
            throw new UnauthorizedAccessException("Account is locked due to multiple failed attempts. Try again later.");
        }

        if (!result.Succeeded)
        {
            _logger.LogWarning("Login failed for user {Username}: invalid password", request.Username);
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        return await GenerateLoginResponseAsync(user, deviceInfo, ipAddress);
    }

    public async Task<LoginResponse> LoginExternalAsync(Guid userId, string? deviceInfo, string? ipAddress)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("User not found or inactive.");
        }

        return await GenerateLoginResponseAsync(user, deviceInfo, ipAddress);
    }

    public async Task<LoginResponse> RegisterAsync(RegisterRequest request)
    {
        var existingUser = await _userManager.FindByNameAsync(request.Username);
        if (existingUser is not null)
        {
            throw new InvalidOperationException("Username is already taken.");
        }

        existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
        {
            throw new InvalidOperationException("Email is already registered.");
        }

        var user = new ApplicationUser
        {
            UserName = request.Username,
            Email = request.Email,
            Department = request.Department,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            TenantId = DefaultTenantId
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            _logger.LogError("User registration failed for {Username}: {Errors}", request.Username, errors);
            throw new InvalidOperationException($"Registration failed: {errors}");
        }

        await _userManager.AddToRoleAsync(user, "Operator");

        _logger.LogInformation("User {Username} registered successfully", request.Username);

        return await GenerateLoginResponseAsync(user, null, null);
    }

    public async Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var principal = _tokenService.GetPrincipalFromExpiredToken(request.AccessToken);
        if (principal is null)
        {
            throw new UnauthorizedAccessException("Invalid access token.");
        }

        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)
                          ?? principal.FindFirst(JwtRegisteredClaimNames.Sub);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid token claims.");
        }

        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken && rt.UserId == userId);

        if (storedToken is null || !storedToken.IsActive)
        {
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");
        }

        storedToken.RevokedAt = DateTimeOffset.UtcNow;
        storedToken.ReplacedByToken = null;

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("User not found or inactive.");
        }

        return await GenerateLoginResponseAsync(user, storedToken.DeviceInfo, storedToken.IpAddress);
    }

    public async Task LogoutAsync(string refreshToken)
    {
        await _tokenService.RevokeRefreshToken(refreshToken);
        _logger.LogInformation("User logged out, refresh token revoked");
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        if (request.NewPassword != request.ConfirmNewPassword)
        {
            throw new InvalidOperationException("New password and confirmation do not match.");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new UnauthorizedAccessException("User not found.");
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Password change failed: {errors}");
        }

        user.MustChangePassword = false;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("Password changed for user {UserId}", userId);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            _logger.LogWarning("Forgot password requested for unknown email {Email}", request.Email);
            return;
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        _logger.LogInformation("Password reset token generated for user {Email}", request.Email);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        if (request.NewPassword != request.ConfirmPassword)
        {
            throw new InvalidOperationException("New password and confirmation do not match.");
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            throw new InvalidOperationException("User not found.");
        }

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Password reset failed: {errors}");
        }

        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("Password reset completed for user {Email}", request.Email);
    }

    public async Task<UserInfo> GetUserInfoAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new UnauthorizedAccessException("User not found.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        return new UserInfo(user.Id, user.UserName ?? string.Empty, user.Email ?? string.Empty, roles.ToArray(), user.TwoFactorEnabled, user.TenantId);
    }

    public async Task<TwoFactorSetupResponse> SetupTwoFactorAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new UnauthorizedAccessException("User not found.");
        }

        var setup = _twoFactorService.GenerateSetup(userId, user.Email ?? string.Empty);

        user.TwoFactorSecret = setup.SharedKey;
        user.RecoveryCodesHash = string.Join(",", setup.RecoveryCodes.Select(c =>
        {
            var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(c));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }));
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("2FA setup initiated for user {UserId}", userId);

        return setup;
    }

    public async Task<bool> VerifyTwoFactorAsync(Guid userId, string code)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || string.IsNullOrEmpty(user.TwoFactorSecret))
        {
            return false;
        }

        var isValid = _twoFactorService.VerifyCode(user.TwoFactorSecret, code);
        if (isValid)
        {
            user.TwoFactorEnabled = true;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await _userManager.UpdateAsync(user);
            _logger.LogInformation("2FA enabled for user {UserId}", userId);
        }

        return isValid;
    }

    public async Task<bool> UseRecoveryCodeAsync(Guid userId, string recoveryCode)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || string.IsNullOrEmpty(user.RecoveryCodesHash))
        {
            return false;
        }

        var hashedCodes = user.RecoveryCodesHash.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var isValid = _twoFactorService.VerifyRecoveryCode(recoveryCode, hashedCodes);

        if (isValid)
        {
            var normalizedInput = recoveryCode.Trim().ToUpperInvariant();
            var inputHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalizedInput))
            ).ToLowerInvariant();

            var remainingCodes = hashedCodes.Where(h => h != inputHash).ToArray();
            user.RecoveryCodesHash = remainingCodes.Length > 0 ? string.Join(",", remainingCodes) : null;
            await _userManager.UpdateAsync(user);
            _logger.LogInformation("Recovery code used for user {UserId}", userId);
        }

        return isValid;
    }

    public async Task<bool> ValidateSessionAsync(string accessToken)
    {
        var principal = _tokenService.GetPrincipalFromExpiredToken(accessToken);
        if (principal is null)
        {
            return false;
        }

        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)
                          ?? principal.FindFirst(JwtRegisteredClaimNames.Sub);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return false;
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user is not null && user.IsActive;
    }

    private async Task<LoginResponse> GenerateLoginResponseAsync(ApplicationUser user, string? deviceInfo, string? ipAddress)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await GetUserPermissionsAsync(user.Id, user.TenantId);
        var (accessToken, expiresAt) = _tokenService.GenerateAccessToken(user, roles, permissions);
        var refreshToken = _tokenService.GenerateRefreshToken();

        var refreshTokenEntity = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
            DeviceInfo = deviceInfo,
            IpAddress = ipAddress
        };

        _context.RefreshTokens.Add(refreshTokenEntity);

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _userManager.UpdateAsync(user);

        await _context.SaveChangesAsync();

        _logger.LogInformation("Login successful for user {UserId}", user.Id);

        return new LoginResponse(
            accessToken,
            refreshToken,
            expiresAt,
            new UserInfo(user.Id, user.UserName ?? string.Empty, user.Email ?? string.Empty, roles.ToArray(), user.TwoFactorEnabled, user.TenantId, permissions)
        );
    }

    private async Task<List<string>> GetUserPermissionsAsync(Guid userId, Guid? tenantId)
    {
        if (!tenantId.HasValue)
            return new List<string>();

        var userRoles = await _sentinelaContext.UserRoles
            .Where(ur => ur.UserId == userId && ur.TenantId == tenantId.Value && !ur.IsDeleted)
            .Select(ur => ur.RoleId)
            .ToListAsync();

        if (!userRoles.Any())
            return new List<string>();

        var permissionCodes = await _sentinelaContext.RolePermissions
            .Where(rp => userRoles.Contains(rp.RoleId) && !rp.IsDeleted)
            .Join(_sentinelaContext.Permissions,
                rp => rp.PermissionId,
                p => p.Id,
                (rp, p) => p.Code)
            .Distinct()
            .ToListAsync();

        return permissionCodes;
    }
}
