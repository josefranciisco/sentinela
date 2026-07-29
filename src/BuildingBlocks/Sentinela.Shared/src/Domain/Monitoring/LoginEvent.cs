using Sentinela.Shared.Core.Entities;

namespace Sentinela.Shared.Domain.Monitoring;

public class LoginEvent : BaseEntity
{
    public Guid ComputerId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string SourceIp { get; set; } = string.Empty;
    public bool IsSuccessful { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
