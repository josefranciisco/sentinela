using Sentinela.Shared.Core.Entities;

namespace Sentinela.Shared.Domain.Identity;

public class Role : BaseEntity
{
    private readonly List<Permission> _permissions = new();

    protected Role() : base() { }

    public Role(string name, string? description = null, bool isSystem = false) : base()
    {
        Name = name;
        Description = description;
        IsSystem = isSystem;
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }
    public bool IsSystem { get; private set; }

    public IReadOnlyList<Permission> Permissions => _permissions.AsReadOnly();

    public void AddPermission(string action, string resource, string? scope = null)
    {
        _permissions.Add(new Permission(action, resource, scope));
    }
}
