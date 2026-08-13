using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sentinela.Identity.Configuration;
using Sentinela.Identity.Models;
using Sentinela.Persistence;
using Sentinela.Shared.Domain.Identity;

namespace Sentinela.Identity.Services;

public class SsoService : ISsoService
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly SsoConfiguration _ssoConfig;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuthService _authService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SentinelaDbContext _sentinelaContext;
    private readonly ILogger<SsoService> _logger;

    public SsoService(
        IOptions<SsoConfiguration> ssoConfig,
        UserManager<ApplicationUser> userManager,
        IAuthService authService,
        IHttpClientFactory httpClientFactory,
        SentinelaDbContext sentinelaContext,
        ILogger<SsoService> logger)
    {
        _ssoConfig = ssoConfig.Value;
        _userManager = userManager;
        _authService = authService;
        _httpClientFactory = httpClientFactory;
        _sentinelaContext = sentinelaContext;
        _logger = logger;
    }

    public bool IsSsoEnabled(string provider) => _ssoConfig.GetProvider(provider) is not null;

    public string BuildLoginUrl(string provider, string state)
    {
        var config = _ssoConfig.GetProvider(provider)
            ?? throw new InvalidOperationException($"SSO provider '{provider}' is not enabled.");

        var redirectUri = BuildRedirectUri(provider);
        var scope = string.Join(" ", config.Scopes);

        return $"{config.AuthorizationEndpoint}" +
               $"?client_id={Uri.EscapeDataString(config.ClientId)}" +
               $"&response_type=code" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
               $"&scope={Uri.EscapeDataString(scope)}" +
               $"&state={Uri.EscapeDataString(state)}";
    }

    public async Task<LoginResponse> HandleSsoCallbackAsync(string provider, string code, string? deviceInfo, string? ipAddress)
    {
        var config = _ssoConfig.GetProvider(provider)
            ?? throw new InvalidOperationException($"SSO provider '{provider}' is not enabled.");

        var token = await ExchangeCodeForTokenAsync(provider, config, code);
        var ssoUser = await FetchUserInfoAsync(provider, config, token);

        if (string.IsNullOrWhiteSpace(ssoUser.Email))
        {
            throw new UnauthorizedAccessException("Não foi possível obter o e-mail do provedor de login.");
        }

        var user = await _userManager.FindByEmailAsync(ssoUser.Email) ?? await _userManager.FindByNameAsync(ssoUser.Username);

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
                TenantId = DefaultTenantId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            var result = await _userManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                _logger.LogError("Failed to create local user from SSO provider {Provider}: {Errors}",
                    provider, string.Join("; ", result.Errors.Select(e => e.Description)));
                throw new InvalidOperationException("Falha ao criar usuário a partir do login externo.");
            }

            await _userManager.AddToRoleAsync(user, "Operator");
            await EnsureSentinelaRoleAsync(user.Id, operatorRoleName: "Operador");
            _logger.LogInformation("Local user created from SSO provider {Provider} for {Email}", provider, ssoUser.Email);
        }
        else
        {
            user.SsoProvider = provider;
            user.SsoSubjectId = ssoUser.SubjectId;
            user.FullName = string.IsNullOrWhiteSpace(user.FullName) ? ssoUser.DisplayName : user.FullName;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await _userManager.UpdateAsync(user);
            await EnsureSentinelaRoleAsync(user.Id, operatorRoleName: "Operador");
        }

        return await _authService.LoginExternalAsync(user.Id, deviceInfo, ipAddress);
    }

    private async Task EnsureSentinelaRoleAsync(Guid userId, string operatorRoleName)
    {
        var hasRole = await _sentinelaContext.UserRoles
            .Where(ur => ur.UserId == userId && ur.TenantId == DefaultTenantId && !ur.IsDeleted)
            .AnyAsync();

        if (hasRole)
            return;

        var roleId = await _sentinelaContext.Roles
            .Where(r => r.TenantId == DefaultTenantId && r.Name == operatorRoleName && !r.IsDeleted)
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        if (roleId != Guid.Empty)
        {
            _sentinelaContext.UserRoles.Add(new UserRole(userId, roleId) { TenantId = DefaultTenantId });
            await _sentinelaContext.SaveChangesAsync();
            _logger.LogInformation("Sentinela role '{Role}' assigned to SSO user {UserId}", operatorRoleName, userId);
        }
        else
        {
            _logger.LogWarning("Sentinela role '{Role}' not found for tenant {TenantId}", operatorRoleName, DefaultTenantId);
        }
    }

    public string BuildRedirectUri(string provider) => $"{_ssoConfig.FrontendBaseUrl.TrimEnd('/')}/api/v1/auth/sso/callback/{provider}";

    private async Task<string> ExchangeCodeForTokenAsync(string provider, SsoProviderConfiguration config, string code)
    {
        using var client = _httpClientFactory.CreateClient("Sso");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = config.ClientId,
            ["client_secret"] = config.ClientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = BuildRedirectUri(provider),
        });

        var response = await client.PostAsync(config.TokenEndpoint, form);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("SSO token exchange failed for {Provider}: {StatusCode} {Body}", provider, response.StatusCode, body);
            throw new UnauthorizedAccessException("Falha ao trocar o código de autorização.");
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
    }

    private async Task<SsoUserInfo> FetchUserInfoAsync(string provider, SsoProviderConfiguration config, string token)
    {
        using var client = _httpClientFactory.CreateClient("Sso");
        var request = new HttpRequestMessage(HttpMethod.Get, config.UserInfoEndpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("SSO userinfo failed for {Provider}: {StatusCode}", provider, response.StatusCode);
            throw new UnauthorizedAccessException("Falha ao obter informações do usuário.");
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        var subjectId = root.TryGetProperty("sub", out var sub) ? sub.GetString()
            : root.TryGetProperty("id", out var id) ? id.GetString()
            : string.Empty;

        string? email = root.TryGetProperty("email", out var emailEl) ? emailEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(email))
        {
            if (root.TryGetProperty("emails", out var emails) && emails.ValueKind == JsonValueKind.Array && emails.GetArrayLength() > 0)
            {
                email = emails[0].GetString();
            }
        }

        var name = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString()
            : root.TryGetProperty("displayName", out var display) ? display.GetString()
            : email;

        var username = email?.Split('@')[0] ?? $"sso_{Guid.NewGuid():N}";

        return new SsoUserInfo(subjectId ?? string.Empty, email ?? string.Empty, username, name ?? string.Empty);
    }
}