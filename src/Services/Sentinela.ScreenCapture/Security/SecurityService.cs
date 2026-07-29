using System.Security.Cryptography;
using Sentinela.ScreenCapture.Interfaces;

namespace Sentinela.ScreenCapture.Security;

public class SecurityService : ISecurityService
{
    public string ComputeHash(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexStringLower(hash);
    }

    public bool ValidateHash(byte[] data, string expectedHash)
    {
        var computed = ComputeHash(data);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(computed),
            Convert.FromHexString(expectedHash));
    }
}
