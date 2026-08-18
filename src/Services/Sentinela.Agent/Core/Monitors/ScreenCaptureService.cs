using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Sentinela.Agent.Configuration;

namespace Sentinela.Agent.Core.Monitors;

public interface IScreenCaptureService
{
    Task<CapturedScreen?> CaptureAsync();
    Task<byte[]?> CaptureEncryptedAsync();
    Task<byte[]?> CaptureCompressedAsync(int quality = 50);
    Task<byte[]?> CaptureForStreamingAsync(int maxWidth = 1920, int quality = 50, int? monitorIndex = null);
    IReadOnlyList<MonitorInfo> GetMonitors();
}

public class ScreenCaptureService : IScreenCaptureService, IDisposable
{
    private readonly AgentOptions _options;
    private readonly ILogger<ScreenCaptureService> _logger;
    private readonly byte[] _encryptionKey;
    private DateTime _lastStreamWarnUtc = DateTime.MinValue;
    
    public ScreenCaptureService(IOptions<AgentOptions> options, ILogger<ScreenCaptureService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _encryptionKey = DeriveEncryptionKey();
    }

    private static byte[] DeriveEncryptionKey()
    {
        try
        {
            return ProtectedData.Unprotect(
                Convert.FromBase64String("AQAAANCMnd8BFdERjHoAwE/Cl+sBAAAAMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDA="),
                null,
                DataProtectionScope.LocalMachine);
        }
        catch
        {
            return SHA256.HashData(Encoding.UTF8.GetBytes(Environment.MachineName));
        }
    }

    public Task<CapturedScreen?> CaptureAsync()
    {
        try
        {
            var bounds = GetScreenBounds();
            using var bitmap = new Bitmap(Math.Max(1, bounds.Width), Math.Max(1, bounds.Height));
            using var graphics = Graphics.FromImage(bitmap);
            CopyScreen(graphics, bounds);
            
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Jpeg);
            var imageData = ms.ToArray();
            
            return Task.FromResult<CapturedScreen?>(new CapturedScreen
            {
                ImageData = imageData,
                Timestamp = DateTime.UtcNow,
                Width = bounds.Width,
                Height = bounds.Height,
                Format = "image/jpeg"
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to capture screen");
            return Task.FromResult<CapturedScreen?>(null);
        }
    }
    
    public async Task<byte[]?> CaptureEncryptedAsync()
    {
        var captured = await CaptureAsync();
        if (captured == null) return null;
        
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.GenerateIV();
        
        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length);
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        {
            cs.Write(captured.ImageData, 0, captured.ImageData.Length);
        }
        return ms.ToArray();
    }
    
    public Task<byte[]?> CaptureCompressedAsync(int quality = 50)
    {
        quality = Math.Clamp(quality, 20, 80);
        return CaptureAsync().ContinueWith(t =>
        {
            var captured = t.Result;
            if (captured == null) return null;
            
            var bounds = GetScreenBounds();
            using var original = new Bitmap(new MemoryStream(captured.ImageData));
            using var resized = new Bitmap(original, bounds.Width / 2, bounds.Height / 2);
            using var ms = new MemoryStream();
            
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
            var jpegCodec = GetEncoderInfo("image/jpeg");
            
            if (jpegCodec != null)
            {
                resized.Save(ms, jpegCodec, encoderParams);
            }
            else
            {
                resized.Save(ms, ImageFormat.Jpeg);
            }
            
            return ms.ToArray();
        });
    }
    
    public Task<byte[]?> CaptureForStreamingAsync(int maxWidth = 1920, int quality = 50, int? monitorIndex = null)
    {
        quality = Math.Clamp(quality, 20, 95);
        try
        {
            var monitors = GetMonitors();
            Rectangle bounds;
            if (monitorIndex.HasValue && monitorIndex >= 0 && monitorIndex < monitors.Count)
            {
                var m = monitors[monitorIndex.Value];
                bounds = new Rectangle(m.X, m.Y, m.Width, m.Height);
            }
            else
            {
                bounds = BoundingRectFromMonitors(monitors);
            }

            using var bitmap = new Bitmap(Math.Max(1, bounds.Width), Math.Max(1, bounds.Height));
            using var graphics = Graphics.FromImage(bitmap);
            CopyScreen(graphics, bounds);

            var scale = maxWidth > 0 && bounds.Width > maxWidth ? (double)maxWidth / bounds.Width : 1.0;
            Bitmap encoded = bitmap;
            Bitmap? resized = null;
            if (scale < 1.0)
            {
                var width = Math.Max(2, (int)(bounds.Width * scale) / 2 * 2);
                var height = Math.Max(2, (int)(bounds.Height * scale) / 2 * 2);
                resized = new Bitmap(width, height);
                using var g = Graphics.FromImage(resized);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.DrawImage(bitmap, 0, 0, width, height);
                encoded = resized;
            }

            using var ms = new MemoryStream();
            try
            {
                var encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                var jpegCodec = GetEncoderInfo("image/jpeg");

                if (jpegCodec != null)
                    encoded.Save(ms, jpegCodec, encoderParams);
                else
                    encoded.Save(ms, ImageFormat.Jpeg);

                return Task.FromResult<byte[]?>(ms.ToArray());
            }
            finally
            {
                resized?.Dispose();
            }
        }
        catch (Exception ex)
        {
            if (DateTime.UtcNow - _lastStreamWarnUtc > TimeSpan.FromSeconds(60))
            {
                _lastStreamWarnUtc = DateTime.UtcNow;
                _logger.LogWarning(ex, "Failed to capture screen for streaming");
            }
            return Task.FromResult<byte[]?>(null);
        }
    }

    public IReadOnlyList<MonitorInfo> GetMonitors()
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
                        monitors.Add(new MonitorInfo(name,
                            rect.Right - rect.Left, rect.Bottom - rect.Top,
                            rect.Left, rect.Top, isPrimary));
                    }
                    return true;
                }, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate monitors");
            monitors.Add(new MonitorInfo("Primary", 1920, 1080, 0, 0, true));
        }
        if (monitors.Count == 0)
            monitors.Add(new MonitorInfo("Primary", 1920, 1080, 0, 0, true));
        return monitors;
    }

    private static Rectangle BoundingRectFromMonitors(IReadOnlyList<MonitorInfo> monitors)
    {
        var minX = int.MaxValue; var minY = int.MaxValue;
        var maxX = int.MinValue; var maxY = int.MinValue;
        foreach (var m in monitors)
        {
            minX = Math.Min(minX, m.X); minY = Math.Min(minY, m.Y);
            maxX = Math.Max(maxX, m.X + m.Width); maxY = Math.Max(maxY, m.Y + m.Height);
        }
        return minX == int.MaxValue ? new Rectangle(0, 0, 1920, 1080)
            : new Rectangle(minX, minY, maxX - minX, maxY - minY);
    }

    private const int SRCCOPY = 0x00CC0020;
    private const int CAPTUREBLT = 0x40000000;

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int w, int h, IntPtr hdcSrc, int xSrc, int ySrc, int rop);

    private static void CopyScreen(Graphics graphics, Rectangle bounds)
    {
        var hdcDest = graphics.GetHdc();
        var hdcSrc = GetDC(IntPtr.Zero);
        var ok = false;
        try
        {
            if (hdcSrc != IntPtr.Zero)
                ok = BitBlt(hdcDest, 0, 0, bounds.Width, bounds.Height, hdcSrc, bounds.X, bounds.Y, SRCCOPY | CAPTUREBLT);
        }
        finally
        {
            if (hdcSrc != IntPtr.Zero)
                ReleaseDC(IntPtr.Zero, hdcSrc);
            graphics.ReleaseHdc(hdcDest);
        }

        if (!ok)
            graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    private Rectangle GetScreenBounds()
    {
        var x = GetSystemMetrics(SM_XVIRTUALSCREEN);
        var y = GetSystemMetrics(SM_YVIRTUALSCREEN);
        var w = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        var h = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        return w > 0 && h > 0
            ? new Rectangle(x, y, w, h)
            : new Rectangle(0, 0, 1920, 1080);
    }
    
    private ImageCodecInfo? GetEncoderInfo(string mimeType)
    {
        return ImageCodecInfo.GetImageEncoders().FirstOrDefault(e => e.MimeType == mimeType);
    }
    
    public void Dispose() { }
}

public class MonitorInfo
{
    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public int X { get; }
    public int Y { get; }
    public bool IsPrimary { get; }

    public MonitorInfo(string name, int width, int height, int x, int y, bool isPrimary)
    {
        Name = name;
        Width = width;
        Height = height;
        X = x;
        Y = y;
        IsPrimary = isPrimary;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct RECT
{
    public int Left, Top, Right, Bottom;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
internal struct MONITORINFOEX
{
    public int cbSize;
    public RECT rcMonitor;
    public RECT rcWork;
    public int dwFlags;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string szDevice;
}

public class CapturedScreen
{
    public byte[] ImageData { get; set; } = Array.Empty<byte>();
    public DateTime Timestamp { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Format { get; set; } = "image/jpeg";
}
