using Sentinela.Shared.Core.Entities;

namespace Sentinela.Persistence.Models;

public class FileTransferRecord : BaseEntity
{
    public Guid SessionId { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public long BytesTransferred { get; set; }
    public string Status { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public string? Checksum { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string TransferredBy { get; set; } = string.Empty;
    public bool IsEncrypted { get; set; } = true;
}
