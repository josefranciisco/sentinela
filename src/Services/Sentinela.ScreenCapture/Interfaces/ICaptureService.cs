namespace Sentinela.ScreenCapture.Interfaces;

public interface ICaptureService
{
    Task<CaptureResult> CaptureAsync(CaptureOptions options, CancellationToken ct = default);
    IReadOnlyList<MonitorInfo> GetMonitors();
}

public record MonitorInfo(string Name, int Width, int Height, int X, int Y, double Scale, bool IsPrimary);

public record CaptureResult(byte[] ImageData, int Width, int Height, string MonitorName, long TimestampMs);

public class CaptureOptions
{
    public int? MonitorIndex { get; set; }
    public int Quality { get; set; } = 80;
    public int MaxWidth { get; set; } = 0;
    public int MaxHeight { get; set; } = 0;
    public bool CaptureAllMonitors { get; set; }
}
