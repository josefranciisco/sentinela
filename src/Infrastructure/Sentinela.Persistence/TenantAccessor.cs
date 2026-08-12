using Sentinela.Shared.Core.Interfaces;

namespace Sentinela.Persistence;

public class TenantAccessor : ITenantAccessor
{
    private Guid _tenantId = Guid.Empty;

    public Guid TenantId => _tenantId;

    public void SetTenantId(Guid tenantId)
    {
        _tenantId = tenantId;
    }
}
