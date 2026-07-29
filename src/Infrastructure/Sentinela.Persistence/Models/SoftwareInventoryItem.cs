using Sentinela.Shared.Core.Entities;

namespace Sentinela.Persistence.Models;

public class SoftwareInventoryItem : BaseEntity
{
    public Guid ComputerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public bool IsAuthorized { get; set; } = true;
    public string Category { get; set; } = string.Empty;
    public DateTimeOffset FirstDetected { get; set; }
    public DateTimeOffset LastDetected { get; set; }
    public string? InstallLocation { get; set; }
}

public class EndpointSecurityStatus : BaseEntity
{
    public Guid ComputerId { get; set; }
    public bool FirewallEnabled { get; set; }
    public bool DefenderEnabled { get; set; }
    public bool AntivirusEnabled { get; set; }
    public bool RealTimeProtectionEnabled { get; set; }
    public int AntivirusSignatureAgeDays { get; set; }
    public DateTimeOffset? AntivirusSignatureLastUpdated { get; set; }
    public string AntivirusProductName { get; set; } = string.Empty;
    public bool BitlockerEnabled { get; set; }
    public bool RdpEnabled { get; set; }
    public DateTimeOffset CollectedAt { get; set; }
}
