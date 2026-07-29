using System.Drawing;
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
}

public class ScreenCaptureService : IScreenCaptureService, IDisposable
{
    private readonly AgentOptions _options;
    private readonly ILogger<ScreenCaptureService> _logger;
    private readonly byte[] _encryptionKey;
    
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
            using var bitmap = new Bitmap(bounds.Width, bounds.Height);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
            
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
    
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

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

public class CapturedScreen
{
    public byte[] ImageData { get; set; } = Array.Empty<byte>();
    public DateTime Timestamp { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Format { get; set; } = "image/jpeg";
}
