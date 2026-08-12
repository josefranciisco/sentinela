namespace Sentinela.Shared.Core.Interfaces;

public interface ITenantAccessor
{
    Guid TenantId { get; }
}
