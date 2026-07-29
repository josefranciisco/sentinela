namespace Sentinela.ScreenCapture.Interfaces;

public interface IThumbnailService
{
    ThumbnailResult GenerateThumbnail(byte[] imageData, int maxWidth = 320, int maxHeight = 180);
}

public record ThumbnailResult(byte[] Data, int Width, int Height, string MimeType);
