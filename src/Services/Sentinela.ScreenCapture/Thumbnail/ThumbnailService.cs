using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using Sentinela.ScreenCapture.Interfaces;

namespace Sentinela.ScreenCapture.Thumbnail;

public class ThumbnailService : IThumbnailService
{
    private readonly ILogger<ThumbnailService> _logger;

    public ThumbnailService(ILogger<ThumbnailService> logger)
    {
        _logger = logger;
    }

    public ThumbnailResult GenerateThumbnail(byte[] imageData, int maxWidth = 320, int maxHeight = 180)
    {
        using var image = Image.Load(imageData);

        var resizeOpts = new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(maxWidth, maxHeight)
        };
        image.Mutate(x => x.Resize(resizeOpts));

        using var ms = new MemoryStream();
        image.Save(ms, new JpegEncoder { Quality = 60 });
        return new ThumbnailResult(ms.ToArray(), image.Width, image.Height, "image/jpeg");
    }
}
