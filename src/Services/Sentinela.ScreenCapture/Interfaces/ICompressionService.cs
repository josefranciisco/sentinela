namespace Sentinela.ScreenCapture.Interfaces;

public enum CompressionFormat { WebP, Jpeg }

public interface ICompressionService
{
    CompressionResult Compress(byte[] imageData, CompressionOptions options);
    bool IsWebPSupported { get; }
}

public record CompressionResult(byte[] Data, string MimeType, long SizeBytes);

public class CompressionOptions
{
    public CompressionFormat Format { get; set; } = CompressionFormat.WebP;
    public int Quality { get; set; } = 100;
    public int MaxWidth { get; set; }
    public int MaxHeight { get; set; }
}
