namespace Sentinela.ScreenCapture.Models;

public class Screenshot
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ComputerId { get; set; } = "";
    public string RequestId { get; set; } = "";
    public string User { get; set; } = "";
    public string MonitorName { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public string Hash { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public string ThumbnailPath { get; set; } = "";
    public string MimeType { get; set; } = "image/jpeg";
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "";
}
