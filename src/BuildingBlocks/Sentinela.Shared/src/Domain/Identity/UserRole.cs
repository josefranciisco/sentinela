using Sentinela.Shared.Core.ValueObjects;

namespace Sentinela.Shared.Domain.Identity;

public class UserRole : ValueObject
{
    public UserRole(Guid userId, Guid roleId, string roleName)
    {
        UserId = userId;
        RoleId = roleId;
        RoleName = roleName;
    }

    public Guid UserId { get; }
    public Guid RoleId { get; }
    public string RoleName { get; }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return UserId;
        yield return RoleId;
    }
}
