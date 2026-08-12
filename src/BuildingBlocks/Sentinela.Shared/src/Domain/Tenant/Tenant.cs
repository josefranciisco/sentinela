using Sentinela.Shared.Core.Entities;

namespace Sentinela.Shared.Domain.Tenant;

public class Tenant : AggregateRoot
{
    public string Name { get; set; } = string.Empty;
    public string? CNPJ { get; set; }
    public TenantPlan Plan { get; set; } = TenantPlan.Starter;
    public TenantStatus Status { get; set; } = TenantStatus.Active;
}
