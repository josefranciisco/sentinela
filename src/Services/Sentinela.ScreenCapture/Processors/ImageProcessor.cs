using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using Sentinela.Shared.Infrastructure.Security;

namespace Sentinela.ScreenCapture.Processors;

public interface IImageProcessor
{
    byte[] CompressImage(byte[] imageData, int quality, int maxWidth, int maxHeight);
    byte[] CreateThumbnail(byte[] imageData, int maxWidth = 320, int maxHeight = 240);
    byte[] EncryptImage(byte[] imageData, string encryptionKey);
    byte[] DecryptImage(byte[] encryptedData, string encryptionKey);
    (int width, int height) GetImageDimensions(byte[] imageData);
}

public class ImageProcessor : IImageProcessor
{
    private readonly ILogger<ImageProcessor> _logger;

    public ImageProcessor(ILogger<ImageProcessor> logger)
    {
        _logger = logger;
    }

    public byte[] CompressImage(byte[] imageData, int quality, int maxWidth, int maxHeight)
    {
        using var image = Image.Load(imageData);

        if (image.Width > maxWidth || image.Height > maxHeight)
        {
            var ratio = Math.Min((double)maxWidth / image.Width, (double)maxHeight / image.Height);
            var newWidth = (int)(image.Width * ratio);
            var newHeight = (int)(image.Height * ratio);
            image.Mutate(x => x.Resize(newWidth, newHeight));
        }

        using var output = new MemoryStream();
        var encoder = new JpegEncoder { Quality = quality };
        image.Save(output, encoder);

        _logger.LogDebug("Image compressed: {OriginalSize} -> {CompressedSize} ({Ratio}%)",
            imageData.Length, output.Length, output.Length * 100 / Math.Max(imageData.Length, 1));

        return output.ToArray();
    }

    public byte[] CreateThumbnail(byte[] imageData, int maxWidth = 320, int maxHeight = 240)
    {
        using var image = Image.Load(imageData);
        image.Mutate(x => x.Resize(maxWidth, maxHeight));

        using var output = new MemoryStream();
        var encoder = new JpegEncoder { Quality = 30 };
        image.Save(output, encoder);

        return output.ToArray();
    }

    public byte[] EncryptImage(byte[] imageData, string encryptionKey)
    {
        var b64 = Convert.ToBase64String(imageData);
        var encrypted = AesEncryption.Encrypt(b64, encryptionKey);
        return System.Text.Encoding.UTF8.GetBytes(encrypted);
    }

    public byte[] DecryptImage(byte[] encryptedData, string encryptionKey)
    {
        var encrypted = System.Text.Encoding.UTF8.GetString(encryptedData);
        var decrypted = AesEncryption.Decrypt(encrypted, encryptionKey);
        return Convert.FromBase64String(decrypted);
    }

    public (int width, int height) GetImageDimensions(byte[] imageData)
    {
        using var image = Image.Load(imageData);
        return (image.Width, image.Height);
    }
}
