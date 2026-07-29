namespace Sentinela.ScreenCapture.Interfaces;

public interface ICacheService
{
    Task<CachedCapture?> GetAsync(string requestId);
    Task SetAsync(string requestId, CachedCapture capture, TimeSpan ttl);
    Task RemoveAsync(string requestId);
    Task<bool> ExistsAsync(string requestId);
}

public class CachedCapture
{
    public byte[] ImageData { get; set; } = Array.Empty<byte>();
    public byte[] ThumbnailData { get; set; } = Array.Empty<byte>();
    public int Width { get; set; }
    public int Height { get; set; }
    public string MonitorName { get; set; } = "";
    public DateTime CapturedAt { get; set; }
}
