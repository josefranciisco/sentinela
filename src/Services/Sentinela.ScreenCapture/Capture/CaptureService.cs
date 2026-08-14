using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Sentinela.ScreenCapture.Interfaces;

namespace Sentinela.ScreenCapture.Capture;

public class CaptureService : ICaptureService
{
    private readonly ILogger<CaptureService> _logger;

    public CaptureService(ILogger<CaptureService> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<MonitorInfo> GetMonitors() => EnumerateMonitors().AsReadOnly();

    public Task<CaptureResult> CaptureAsync(CaptureOptions options, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var monitors = EnumerateMonitors();

        if (options.CaptureAllMonitors || monitors.Count == 0)
        {
            var rect = BoundingRectFromMonitors(monitors);
            var (data, w, h) = CaptureGdi(rect.X, rect.Y, rect.Width, rect.Height);
            var monitorLabel = monitors.Count <= 1
                ? (monitors.FirstOrDefault()?.Name ?? "Primary")
                : $"{monitors.Count} Monitores";
            sw.Stop();
            return Task.FromResult(new CaptureResult(data, w, h, monitorLabel, sw.ElapsedMilliseconds));
        }

        var index = options.MonitorIndex;
        var monitor = index.HasValue && index >= 0 && index < monitors.Count
            ? monitors[index.Value]
            : monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
        var labelIndex = monitors.IndexOf(monitor);
        var label = labelIndex >= 0
            ? $"Monitor {labelIndex + 1}{(monitor.IsPrimary ? " (principal)" : "")}"
            : monitor.Name;

        var (imgData, width, height) = CaptureGdi(monitor.X, monitor.Y, monitor.Width, monitor.Height);
        sw.Stop();
        return Task.FromResult(new CaptureResult(imgData, width, height, label, sw.ElapsedMilliseconds));
    }

    private static (byte[] data, int width, int height) CaptureGdi(int x, int y, int width, int height)
    {
        using var bitmap = new System.Drawing.Bitmap(width, height);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height));
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Jpeg);
        return (ms.ToArray(), width, height);
    }

    private static MonitorRect BoundingRectFromMonitors(IReadOnlyList<MonitorInfo> monitors)
    {
        var minX = int.MaxValue; var minY = int.MaxValue;
        var maxX = int.MinValue; var maxY = int.MinValue;
        foreach (var m in monitors)
        {
            minX = Math.Min(minX, m.X); minY = Math.Min(minY, m.Y);
            maxX = Math.Max(maxX, m.X + m.Width); maxY = Math.Max(maxY, m.Y + m.Height);
        }
        if (minX == int.MaxValue) return new MonitorRect(0, 0, 1920, 1080);
        return new MonitorRect(minX, minY, maxX - minX, maxY - minY);
    }

    private static List<MonitorInfo> EnumerateMonitors()
    {
        var monitors = new List<MonitorInfo>();
        try
        {
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                (hMonitor, hdcMonitor, lprcMonitor, dwData) =>
                {
                    var mi = new MONITORINFOEX();
                    mi.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
                    if (GetMonitorInfo(hMonitor, ref mi))
                    {
                        var name = new string(mi.szDevice).TrimEnd('\0');
                        var rect = mi.rcMonitor;
                        var isPrimary = (mi.dwFlags & 1) != 0;
                        var scale = GetDpiForMonitor(hMonitor);
                        monitors.Add(new MonitorInfo(name,
                            rect.Right - rect.Left, rect.Bottom - rect.Top,
                            rect.Left, rect.Top,
                            scale, isPrimary));
                    }
                    return true;
                }, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            monitors.Add(new MonitorInfo("Primary", 1920, 1080, 0, 0, 1.0, true));
        }
        return monitors;
    }

    private static double GetDpiForMonitor(IntPtr hMonitor)
    {
        try
        {
            var hdc = GetDC(IntPtr.Zero);
            if (hdc != IntPtr.Zero)
            {
                var dpi = GetDeviceCaps(hdc, 88);
                ReleaseDC(IntPtr.Zero, hdc);
                return dpi / 96.0;
            }
        }
        catch { }
        return 1.0;
    }

    private record MonitorRect(int X, int Y, int Width, int Height);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);
}
