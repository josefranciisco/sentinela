using Sentinela.Shared.Core.ValueObjects;

namespace Sentinela.Shared.Domain.Identity;

public class RefreshToken : ValueObject
{
    public RefreshToken(string token, DateTimeOffset expiresAt)
    {
        Token = token;
        ExpiresAt = expiresAt;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public string Token { get; }
    public DateTimeOffset ExpiresAt { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? RevokedAt { get; }
    public string? ReplacedByToken { get; }

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt is not null;
    public bool IsActive => !IsExpired && !IsRevoked;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Token;
    }
}
