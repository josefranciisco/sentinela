namespace Sentinela.ScreenCapture.Interfaces;

public interface IUploadService
{
    Task<UploadResult> UploadAsync(ScreenshotUpload upload, CancellationToken ct = default);
    Task<int> GetPendingCountAsync();
}

public record UploadResult(bool Success, string? ScreenshotId, string? ErrorMessage, long ElapsedMs);

public class ScreenshotUpload
{
    public string ComputerId { get; set; } = "";
    public string RequestId { get; set; } = "";
    public string MonitorName { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public byte[] ImageData { get; set; } = Array.Empty<byte>();
    public byte[] ThumbnailData { get; set; } = Array.Empty<byte>();
    public string ImageMimeType { get; set; } = "image/jpeg";
    public string User { get; set; } = "";
    public string Hash { get; set; } = "";
    public long TimestampMs { get; set; }
}
