using Sentinela.Shared.Core.Entities;

namespace Sentinela.Shared.Domain.Identity;

public class Role : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsSystemRole { get; private set; }
    public bool IsDefault { get; private set; }

    private readonly List<RolePermission> _rolePermissions = new();
    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions.AsReadOnly();

    private Role() { }

    public Role(string name, string? description = null, bool isSystemRole = false, bool isDefault = false)
    {
        Name = name;
        Description = description;
        IsSystemRole = isSystemRole;
        IsDefault = isDefault;
    }

    public void UpdateName(string name)
    {
        if (IsSystemRole)
            throw new InvalidOperationException("Cannot rename a system role.");
        Name = name;
    }

    public void UpdateDescription(string? description)
    {
        Description = description;
    }

    public void SetPermissions(List<RolePermission> permissions)
    {
        _rolePermissions.Clear();
        _rolePermissions.AddRange(permissions);
    }

    public void AddPermission(RolePermission permission)
    {
        if (_rolePermissions.Any(p => p.PermissionId == permission.PermissionId))
            return;
        _rolePermissions.Add(permission);
    }

    public void RemovePermission(Guid permissionId)
    {
        var existing = _rolePermissions.FirstOrDefault(p => p.PermissionId == permissionId);
        if (existing is not null)
            _rolePermissions.Remove(existing);
    }
}
