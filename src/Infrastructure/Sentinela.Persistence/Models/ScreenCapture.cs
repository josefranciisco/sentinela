using Sentinela.Shared.Core.Entities;

namespace Sentinela.Persistence.Models;

public enum CaptureStatus
{
    Pending,
    Captured,
    Failed,
    Expired
}

public class ScreenCapture : BaseEntity
{
    public Guid ComputerId { get; set; }
    public byte[]? ImageData { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public CaptureStatus Status { get; set; }
    public string? RequestedBy { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
}
