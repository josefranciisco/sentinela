namespace Sentinela.RemoteAssistance.Core;

public interface IFileTransferService
{
    Task<FileTransfer> CreateTransferAsync(Guid sessionId, TransferDirection direction, string fileName, long fileSize, string sourcePath, string destinationPath, string transferredBy);
    Task<FileTransfer?> GetTransferAsync(Guid transferId);
    Task<IEnumerable<FileTransfer>> GetTransfersBySessionAsync(Guid sessionId);
    Task<bool> UpdateTransferProgressAsync(Guid transferId, long bytesTransferred);
    Task<bool> CompleteTransferAsync(Guid transferId, string checksum);
    Task<bool> FailTransferAsync(Guid transferId, string error);
    Task<bool> CancelTransferAsync(Guid transferId);
    Task<string> ComputeChecksumAsync(Stream stream);
    Task<IReadOnlyList<byte[]>> ChunkFileAsync(Stream fileStream, int chunkSize = 81920);
    Task<bool> ValidateFileTransferAsync(string fileName, long fileSize);
}
