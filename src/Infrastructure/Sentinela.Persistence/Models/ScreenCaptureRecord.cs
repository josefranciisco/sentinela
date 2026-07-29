using Sentinela.Shared.Core.Entities;

namespace Sentinela.Persistence.Models;

public class ScreenCaptureRecord : BaseEntity
{
    protected ScreenCaptureRecord() : base() { }

    public ScreenCaptureRecord(Guid computerId, DateTimeOffset capturedAt, string filePath, long fileSize, string? username = null)
        : base()
    {
        ComputerId = computerId;
        CapturedAt = capturedAt;
        FilePath = filePath;
        FileSize = fileSize;
        Username = username;
    }

    public Guid ComputerId { get; private set; }
    public DateTimeOffset CapturedAt { get; private set; }
    public string FilePath { get; private set; }
    public long FileSize { get; private set; }
    public string? Username { get; private set; }
    public string? ThumbnailPath { get; private set; }
    public bool IsProcessed { get; private set; }
}
