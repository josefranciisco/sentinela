using Sentinela.Shared.Core.Entities;

namespace Sentinela.Persistence.Models;

public class Screenshot : BaseEntity
{
    public Guid ComputerId { get; set; }
    public string RequestId { get; set; } = "";
    public string User { get; set; } = "";
    public string MonitorName { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public string Hash { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public string? ThumbnailPath { get; set; }
    public string MimeType { get; set; } = "image/jpeg";
    public long Size { get; set; }
    public string CreatedBy { get; set; } = "";
}
