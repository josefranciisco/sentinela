using System.Security.Cryptography;
using System.Text;
using OtpNet;
using Sentinela.Identity.Models;

namespace Sentinela.Identity.Services;

public class TwoFactorService : ITwoFactorService
{
    private readonly ILogger<TwoFactorService> _logger;

    public TwoFactorService(ILogger<TwoFactorService> logger)
    {
        _logger = logger;
    }

    public TwoFactorSetupResponse GenerateSetup(Guid userId, string email)
    {
        var key = KeyGeneration.GenerateRandomKey(20);
        var sharedKey = Base32Encoding.ToString(key);

        var issuer = Uri.EscapeDataString("Sentinela");
        var escapedEmail = Uri.EscapeDataString(email);
        var qrCodeUri = $"otpauth://totp/{issuer}:{escapedEmail}?secret={sharedKey}&issuer={issuer}&algorithm=SHA256&digits=6&period=30";

        var recoveryCodes = GenerateRecoveryCodes(10);

        return new TwoFactorSetupResponse(sharedKey, qrCodeUri, recoveryCodes);
    }

    public bool VerifyCode(string secret, string code)
    {
        try
        {
            var key = Base32Encoding.ToBytes(secret);
            var totp = new Totp(key, step: 30, mode: OtpHashMode.Sha256, totpSize: 6);
            long timeStepSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            for (long i = -2; i <= 2; i++)
            {
                var stepTime = DateTimeOffset.FromUnixTimeSeconds(timeStepSeconds + (i * 30)).DateTime;
                if (totp.ComputeTotp(stepTime) == code)
                    return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TOTP verification failed");
            return false;
        }
    }

    public string[] GenerateRecoveryCodes(int count = 10)
    {
        var codes = new string[count];
        using var rng = RandomNumberGenerator.Create();

        for (var i = 0; i < count; i++)
        {
            var bytes = new byte[5];
            rng.GetBytes(bytes);
            var code = BitConverter.ToString(bytes).Replace("-", "").ToUpperInvariant();
            codes[i] = $"{code[..5]}-{code[5..]}";
        }

        return codes;
    }

    public bool VerifyRecoveryCode(string code, string[] hashedRecoveryCodes)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        var hashedInput = BCryptOrSha256(normalizedCode);
        return hashedRecoveryCodes.Any(h => h == hashedInput);
    }

    private static string BCryptOrSha256(string code)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
