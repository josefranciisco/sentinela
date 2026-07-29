using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Sentinela.Identity.Configuration;
using Sentinela.Identity.Models;

namespace Sentinela.Identity.Services;

public class SsoService : ISsoService
{
    private readonly SsoConfiguration _ssoConfig;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuthService _authService;
    private readonly ILogger<SsoService> _logger;

    public SsoService(
        IOptions<SsoConfiguration> ssoConfig,
        UserManager<ApplicationUser> userManager,
        IAuthService authService,
        ILogger<SsoService> logger)
    {
        _ssoConfig = ssoConfig.Value;
        _userManager = userManager;
        _authService = authService;
        _logger = logger;
    }

    public async Task<LoginResponse> HandleSsoCallbackAsync(string provider, string code, string? deviceInfo, string? ipAddress)
    {
        if (!IsSsoEnabled(provider))
        {
            throw new InvalidOperationException($"SSO provider '{provider}' is not enabled.");
        }

        var ssoUser = await GetUserFromExternalLoginAsync(provider, code);
        if (ssoUser is null)
        {
            throw new UnauthorizedAccessException("Failed to retrieve user information from SSO provider.");
        }

        var user = await _userManager.FindByEmailAsync(ssoUser.Email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = ssoUser.Username,
                Email = ssoUser.Email,
                FullName = ssoUser.DisplayName,
                IsActive = true,
                SsoProvider = provider,
                SsoSubjectId = ssoUser.SubjectId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            var result = await _userManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException("Failed to create local user from SSO.");
            }

            await _userManager.AddToRoleAsync(user, "User");
            _logger.LogInformation("Local user created from SSO provider {Provider} for {Email}", provider, ssoUser.Email);
        }
        else
        {
            user.SsoProvider = provider;
            user.SsoSubjectId = ssoUser.SubjectId;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await _userManager.UpdateAsync(user);
        }

        return await _authService.LoginAsync(
            new LoginRequest(user.UserName ?? string.Empty, user.Email ?? string.Empty, string.Empty, null, null),
            deviceInfo,
            ipAddress
        );
    }

    public string GetSsoLoginUrl(string provider)
    {
        if (!IsSsoEnabled(provider))
        {
            throw new InvalidOperationException($"SSO provider '{provider}' is not enabled.");
        }

        return $"{_ssoConfig.Authority}/authorize?client_id={_ssoConfig.ClientId}&response_type=code&redirect_uri={_ssoConfig.CallbackPath}&scope={string.Join(" ", _ssoConfig.Scopes)}";
    }

    public bool IsSsoEnabled(string provider)
    {
        return _ssoConfig.Enabled && string.Equals(provider, _ssoConfig.DefaultScheme, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<SsoUserInfo?> GetUserFromExternalLoginAsync(string provider, string subjectId)
    {
        var user = await _userManager.FindByEmailAsync(subjectId);
        if (user is not null && user.SsoProvider == provider)
        {
            return new SsoUserInfo(
                user.SsoSubjectId ?? subjectId,
                user.Email ?? string.Empty,
                user.UserName ?? string.Empty,
                user.FullName ?? string.Empty
            );
        }

        return null;
    }
}
