namespace Sentinela.ScreenCapture.Core;

public class ScreenCapturePolicy
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public CaptureMode Mode { get; set; } = CaptureMode.OnDemand;
    public int Quality { get; set; } = 50;
    public int MaxWidth { get; set; } = 1920;
    public int MaxHeight { get; set; } = 1080;
    public int IntervalSeconds { get; set; } = 300;
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(30);
    public string[] TriggerEvents { get; set; } = Array.Empty<string>();
    public string[] TargetComputers { get; set; } = Array.Empty<string>();
    public string[] TargetUsers { get; set; } = Array.Empty<string>();
    public string[] ExcludedComputers { get; set; } = Array.Empty<string>();
    public bool RequireJustification { get; set; } = true;
    public bool RequireApproval { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public enum CaptureMode
{
    OnDemand,
    Scheduled,
    EventDriven,
    Continuous
}

public class CaptureRequest
{
    public Guid Id { get; set; }
    public Guid ComputerId { get; set; }
    public Guid RequestedBy { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public string Justification { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; }
    public CaptureRequestStatus Status { get; set; } = CaptureRequestStatus.Pending;
    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? CapturedAt { get; set; }
}

public enum CaptureRequestStatus
{
    Pending,
    Approved,
    Denied,
    Captured,
    Failed,
    Expired
}
