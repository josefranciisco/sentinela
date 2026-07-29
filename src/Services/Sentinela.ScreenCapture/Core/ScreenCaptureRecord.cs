namespace Sentinela.ScreenCapture.Core;

public class ScreenCaptureRecord
{
    public Guid Id { get; set; }
    public Guid ComputerId { get; set; }
    public string ComputerName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTimeOffset CapturedAt { get; set; }
    public DateTimeOffset? ReceivedAt { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int Quality { get; set; }
    public long SizeBytes { get; set; }
    public string Format { get; set; } = "jpeg";
    public bool IsEncrypted { get; set; } = true;
    public Guid PolicyId { get; set; }
    public CaptureMode Mode { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public string Justification { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string ThumbnailPath { get; set; } = string.Empty;
    public DateTimeOffset? DeletedAt { get; set; }
}
