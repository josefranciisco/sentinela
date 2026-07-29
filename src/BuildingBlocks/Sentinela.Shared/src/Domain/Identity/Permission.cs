using Sentinela.Shared.Core.ValueObjects;

namespace Sentinela.Shared.Domain.Identity;

public class Permission : ValueObject
{
    public Permission(string action, string resource, string? scope = null)
    {
        Action = action;
        Resource = resource;
        Scope = scope;
    }

    public string Action { get; }
    public string Resource { get; }
    public string? Scope { get; }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Action;
        yield return Resource;
        yield return Scope ?? string.Empty;
    }
}
