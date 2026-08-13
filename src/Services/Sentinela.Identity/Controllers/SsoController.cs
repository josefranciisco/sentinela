using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Sentinela.Identity.Configuration;
using Sentinela.Identity.Services;

namespace Sentinela.Identity.Controllers;

[ApiController]
[Route("api/auth/sso")]
public class SsoController : ControllerBase
{
    private readonly ISsoService _ssoService;
    private readonly SsoConfiguration _ssoConfig;
    private readonly ILogger<SsoController> _logger;

    public SsoController(ISsoService ssoService, IOptions<SsoConfiguration> ssoConfig, ILogger<SsoController> logger)
    {
        _ssoService = ssoService;
        _ssoConfig = ssoConfig.Value;
        _logger = logger;
    }

    [HttpGet("login/{provider}")]
    public IActionResult Login(string provider)
    {
        if (!_ssoService.IsSsoEnabled(provider))
        {
            return RedirectError($"Provedor de login '{provider}' não está configurado.");
        }

        var state = Guid.NewGuid().ToString("N");
        Response.Cookies.Append("sso_state", state, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            MaxAge = TimeSpan.FromMinutes(10),
        });

        var loginUrl = _ssoService.BuildLoginUrl(provider, state);
        return Redirect(loginUrl);
    }

    [HttpGet("callback/{provider}")]
    public async Task<IActionResult> Callback(string provider, [FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, [FromQuery] string? error_description)
    {
        if (!string.IsNullOrEmpty(error))
        {
            return RedirectError(error_description ?? "O usuário cancelou o login ou ocorreu um erro no provedor.");
        }

        if (!_ssoService.IsSsoEnabled(provider))
        {
            return RedirectError($"Provedor de login '{provider}' não está configurado.");
        }

        if (string.IsNullOrEmpty(code))
        {
            return RedirectError("Autorização negada — nenhum código foi retornado pelo provedor.");
        }

        var expectedState = Request.Cookies["sso_state"];
        if (string.IsNullOrEmpty(expectedState) || !string.Equals(expectedState, state, StringComparison.Ordinal))
        {
            return RedirectError("Estado de segurança inválido. Tente novamente.");
        }

        Response.Cookies.Delete("sso_state");

        try
        {
            var deviceInfo = Request.Headers.UserAgent.ToString();
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var response = await _ssoService.HandleSsoCallbackAsync(provider, code, deviceInfo, ipAddress);

            var userJson = System.Text.Json.JsonSerializer.Serialize(response.User);
            var redirectUrl = $"{_ssoConfig.FrontendBaseUrl.TrimEnd('/')}/sso-callback" +
                $"?accessToken={Uri.EscapeDataString(response.AccessToken)}" +
                $"&refreshToken={Uri.EscapeDataString(response.RefreshToken)}" +
                $"&expiresAt={response.ExpiresAt.ToUnixTimeSeconds()}" +
                $"&user={Uri.EscapeDataString(userJson)}";

            return Redirect(redirectUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SSO callback failed for provider {Provider}", provider);
            return RedirectError(ex.Message);
        }
    }

    private IActionResult RedirectError(string message) =>
        Redirect($"{_ssoConfig.FrontendBaseUrl.TrimEnd('/')}/sso-callback?error={Uri.EscapeDataString(message)}");
}