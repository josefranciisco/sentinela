using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentinela.Identity.Models;
using Sentinela.Identity.Services;

namespace Sentinela.Identity.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var deviceInfo = Request.Headers.UserAgent.ToString();
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var response = await _authService.LoginAsync(request, deviceInfo, ipAddress);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Login failed: {Message}", ex.Message);
            return Unauthorized(new AuthErrorResponse("AUTH_FAILED", ex.Message, null));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Login failed: {Message}", ex.Message);
            return BadRequest(new AuthErrorResponse("2FA_REQUIRED", ex.Message, null));
        }
    }

    [HttpPost("register")]
    public async Task<ActionResult<LoginResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var response = await _authService.RegisterAsync(request);
            _logger.LogInformation("User registered: {Username}", request.Username);
            return CreatedAtAction(nameof(GetCurrentUser), response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new AuthErrorResponse("REGISTRATION_FAILED", ex.Message, null));
        }
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var response = await _authService.RefreshTokenAsync(request);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new AuthErrorResponse("TOKEN_INVALID", ex.Message, null));
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] string refreshToken)
    {
        await _authService.LogoutAsync(refreshToken);
        _logger.LogInformation("User logged out");
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            var userId = GetUserId();
            await _authService.ChangePasswordAsync(userId, request);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new AuthErrorResponse("PASSWORD_CHANGE_FAILED", ex.Message, null));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new AuthErrorResponse("USER_NOT_FOUND", ex.Message, null));
        }
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        await _authService.ForgotPasswordAsync(request);
        return Ok(new { message = "If the email exists, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        try
        {
            await _authService.ResetPasswordAsync(request);
            return Ok(new { message = "Password has been reset successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new AuthErrorResponse("RESET_FAILED", ex.Message, null));
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserInfo>> GetCurrentUser()
    {
        var userId = GetUserId();
        var userInfo = await _authService.GetUserInfoAsync(userId);
        return Ok(userInfo);
    }

    [HttpPost("2fa/setup")]
    [Authorize]
    public async Task<ActionResult<TwoFactorSetupResponse>> SetupTwoFactor()
    {
        var userId = GetUserId();
        var setup = await _authService.SetupTwoFactorAsync(userId);
        return Ok(setup);
    }

    [HttpPost("2fa/verify")]
    [Authorize]
    public async Task<IActionResult> VerifyTwoFactor([FromBody] TwoFactorVerifyRequest request)
    {
        var userId = GetUserId();
        var result = await _authService.VerifyTwoFactorAsync(userId, request.Code);
        if (!result)
        {
            return BadRequest(new AuthErrorResponse("2FA_VERIFICATION_FAILED", "Invalid code.", null));
        }
        return Ok(new { message = "Two-factor authentication enabled successfully." });
    }

    [HttpPost("2fa/recover")]
    [Authorize]
    public async Task<ActionResult<LoginResponse>> RecoverWithCode([FromBody] TwoFactorRecoveryRequest request)
    {
        var userId = GetUserId();
        var result = await _authService.UseRecoveryCodeAsync(userId, request.RecoveryCode);
        if (!result)
        {
            return BadRequest(new AuthErrorResponse("RECOVERY_FAILED", "Invalid recovery code.", null));
        }
        return Ok(new { message = "Recovery code used successfully." });
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
                          ?? User.FindFirst("sub");
        if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user token.");
        }
        return userId;
    }
}
