using Sentinela.Shared.Core.Entities;

namespace Sentinela.Shared.Domain.Identity;

public class User : AggregateRoot
{
    private readonly List<UserRole> _roles = new();
    private readonly List<RefreshToken> _refreshTokens = new();

    protected User() : base() { }

    public User(string username, string email, string passwordHash) : base()
    {
        Username = username;
        Email = email;
        PasswordHash = passwordHash;
        IsActive = true;
        IsLocked = false;
        LoginAttempts = 0;
        TwoFactorEnabled = false;
    }

    public string Username { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsLocked { get; private set; }
    public DateTimeOffset? LockoutEnd { get; private set; }
    public int LoginAttempts { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public bool TwoFactorEnabled { get; private set; }
    public string? TwoFactorSecret { get; private set; }

    public IReadOnlyList<UserRole> Roles => _roles.AsReadOnly();
    public IReadOnlyList<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    public void Lock(DateTimeOffset? lockoutEnd = null)
    {
        IsLocked = true;
        LockoutEnd = lockoutEnd;
    }

    public void Unlock()
    {
        IsLocked = false;
        LockoutEnd = null;
        LoginAttempts = 0;
    }

    public void IncrementLoginAttempts()
    {
        LoginAttempts++;
    }

    public void ResetLoginAttempts()
    {
        LoginAttempts = 0;
    }

    public void AddRole(Guid roleId)
    {
        if (_roles.Any(r => r.RoleId == roleId)) return;
        _roles.Add(new UserRole(Id, roleId));
    }

    public void RemoveRole(Guid roleId)
    {
        var role = _roles.FirstOrDefault(r => r.RoleId == roleId);
        if (role is not null)
            _roles.Remove(role);
    }
}
