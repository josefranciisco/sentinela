using Sentinela.Shared.Core.Entities;

namespace Sentinela.Shared.Domain.Monitoring;

public class ServiceEvent : BaseEntity
{
    public Guid ComputerId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}
