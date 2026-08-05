using Sentinela.Shared.Core.Entities;

namespace Sentinela.Persistence.Models;

public class RemoteSession : BaseEntity
{
    public Guid ComputerId { get; set; }
    public string? RequestedBy { get; set; }
    public string SessionType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? TerminatedAt { get; set; }
    public string? TerminatedBy { get; set; }
    public int? MonitorIndex { get; set; }
}
