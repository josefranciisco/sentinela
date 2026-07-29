namespace Sentinela.RemoteAssistance.Core;

public class FileTransfer
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public TransferDirection Direction { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public long BytesTransferred { get; set; }
    public TransferStatus Status { get; set; }
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public string? Checksum { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string TransferredBy { get; set; } = string.Empty;
    public bool IsEncrypted { get; set; } = true;
}

public enum TransferDirection
{
    Upload,
    Download
}

public enum TransferStatus
{
    Pending,
    Transferring,
    Paused,
    Completed,
    Failed,
    Cancelled
}
