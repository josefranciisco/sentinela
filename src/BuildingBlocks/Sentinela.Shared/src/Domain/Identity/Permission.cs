using Sentinela.Shared.Core.Entities;

namespace Sentinela.Shared.Domain.Identity;

public class Permission : BaseEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;

    private Permission() { }

    public Permission(string code, string name, string description, string category)
    {
        Code = code;
        Name = name;
        Description = description;
        Category = category;
    }
}
