namespace Sentinela.Shared.Core.Interfaces;

public interface ICurrentUser
{
    string UserId { get; }
    string Username { get; }
    string Email { get; }
    string[] Roles { get; }
    bool IsAuthenticated { get; }
    string IpAddress { get; }
    Guid TenantId { get; }
}
