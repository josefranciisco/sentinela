using Sentinela.Shared.Core.Entities;

namespace Sentinela.Shared.Domain.Identity;

public class RolePermission : BaseEntity
{
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public Role? Role { get; private set; }
    public Permission? Permission { get; private set; }

    private RolePermission() { }

    public RolePermission(Guid roleId, Guid permissionId)
    {
        RoleId = roleId;
        PermissionId = permissionId;
    }
}
