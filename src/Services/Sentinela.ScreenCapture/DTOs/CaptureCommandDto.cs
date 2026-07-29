namespace Sentinela.ScreenCapture.DTOs;

public class CaptureCommandDto
{
    public string Command { get; set; } = "CaptureScreen";
    public string ComputerId { get; set; } = "";
    public string RequestId { get; set; } = "";
    public int? MonitorIndex { get; set; }
    public int Quality { get; set; } = 80;
    public string? Reason { get; set; }
    public string? RequestedBy { get; set; }
    public bool CaptureAllMonitors { get; set; }
}

public class CaptureResultDto
{
    public string RequestId { get; set; } = "";
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public long CaptureTimeMs { get; set; }
    public long UploadTimeMs { get; set; }
    public string? ScreenshotId { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string MonitorName { get; set; } = "";
    public string MimeType { get; set; } = "image/jpeg";
    public byte[]? ImageData { get; set; }
    public byte[]? ThumbnailData { get; set; }
}
