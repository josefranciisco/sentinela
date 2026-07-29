using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using Sentinela.ScreenCapture.Interfaces;

namespace Sentinela.ScreenCapture.Compression;

public class CompressionService : ICompressionService
{
    private readonly ILogger<CompressionService> _logger;
    private readonly bool _webPSupported;

    public bool IsWebPSupported => _webPSupported;

    public CompressionService(ILogger<CompressionService> logger)
    {
        _logger = logger;
        try
        {
            using var img = new Image<Rgba32>(1, 1);
            var encoder = new WebpEncoder();
            _webPSupported = true;
        }
        catch
        {
            _webPSupported = false;
        }
    }

    public CompressionResult Compress(byte[] imageData, CompressionOptions options)
    {
        using var image = Image.Load(imageData);
        var format = options.Format;

        if (format == CompressionFormat.WebP && !_webPSupported)
        {
            _logger.LogDebug("WebP not supported, falling back to JPEG");
            format = CompressionFormat.Jpeg;
        }

        if (options.MaxWidth > 0 || options.MaxHeight > 0)
        {
            var resizeOpts = new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(
                    options.MaxWidth > 0 ? options.MaxWidth : image.Width,
                    options.MaxHeight > 0 ? options.MaxHeight : image.Height)
            };
            image.Mutate(x => x.Resize(resizeOpts));
        }

        using var ms = new MemoryStream();
        string mimeType;

        switch (format)
        {
            case CompressionFormat.WebP:
                image.Save(ms, new WebpEncoder { Quality = options.Quality });
                mimeType = "image/webp";
                break;
            default:
                image.Save(ms, new JpegEncoder { Quality = options.Quality });
                mimeType = "image/jpeg";
                break;
        }

        var data = ms.ToArray();
        return new CompressionResult(data, mimeType, data.Length);
    }
}
