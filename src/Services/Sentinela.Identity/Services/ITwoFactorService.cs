using Sentinela.Identity.Models;

namespace Sentinela.Identity.Services;

public interface ITwoFactorService
{
    TwoFactorSetupResponse GenerateSetup(Guid userId, string email);
    bool VerifyCode(string secret, string code);
    string[] GenerateRecoveryCodes(int count = 10);
    bool VerifyRecoveryCode(string code, string[] hashedRecoveryCodes);
}
