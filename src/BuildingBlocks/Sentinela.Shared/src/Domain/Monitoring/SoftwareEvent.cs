using Sentinela.Shared.Core.Entities;

namespace Sentinela.Shared.Domain.Monitoring;

public class SoftwareEvent : BaseEntity
{
    public Guid ComputerId { get; set; }
    public string SoftwareName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}
