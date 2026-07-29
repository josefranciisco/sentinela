using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sentinela.RemoteAssistance.Configuration;

namespace Sentinela.RemoteAssistance.Core;

public class FileTransferService : IFileTransferService
{
    private readonly ConcurrentDictionary<Guid, FileTransfer> _transfers = new();
    private readonly ILogger<FileTransferService> _logger;
    private readonly RemoteAssistanceOptions _options;

    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bat", ".cmd", ".ps1", ".vbs", ".scr", ".com", ".pif",
        ".reg", ".msi", ".msp", ".jar", ".wsf", ".wsh", ".sh", ".appref-ms"
    };

    public FileTransferService(IOptions<RemoteAssistanceOptions> options, ILogger<FileTransferService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<FileTransfer> CreateTransferAsync(Guid sessionId, TransferDirection direction, string fileName, long fileSize, string sourcePath, string destinationPath, string transferredBy)
    {
        if (!_options.Enabled)
            throw new InvalidOperationException("Remote assistance is disabled.");

        var maxBytes = _options.MaxFileTransferSizeMB * 1024L * 1024L;
        if (fileSize > maxBytes)
            throw new InvalidOperationException($"File size {fileSize} exceeds maximum allowed {maxBytes} bytes.");

        var transfer = new FileTransfer
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Direction = direction,
            FileName = fileName,
            FileSize = fileSize,
            BytesTransferred = 0,
            Status = TransferStatus.Pending,
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            StartedAt = DateTimeOffset.UtcNow,
            TransferredBy = transferredBy,
            IsEncrypted = true
        };

        _transfers.TryAdd(transfer.Id, transfer);
        _logger.LogInformation("File transfer created: {TransferId} file={FileName} size={FileSize} direction={Direction}",
            transfer.Id, fileName, fileSize, direction);

        return Task.FromResult(transfer);
    }

    public Task<FileTransfer?> GetTransferAsync(Guid transferId)
    {
        _transfers.TryGetValue(transferId, out var transfer);
        return Task.FromResult(transfer);
    }

    public Task<IEnumerable<FileTransfer>> GetTransfersBySessionAsync(Guid sessionId)
    {
        var result = _transfers.Values.Where(t => t.SessionId == sessionId).OrderBy(t => t.StartedAt);
        return Task.FromResult(result);
    }

    public Task<bool> UpdateTransferProgressAsync(Guid transferId, long bytesTransferred)
    {
        if (!_transfers.TryGetValue(transferId, out var transfer))
            return Task.FromResult(false);

        transfer.BytesTransferred = bytesTransferred;
        transfer.Status = bytesTransferred >= transfer.FileSize ? TransferStatus.Completed : TransferStatus.Transferring;

        if (transfer.Status == TransferStatus.Completed)
            transfer.CompletedAt = DateTimeOffset.UtcNow;

        return Task.FromResult(true);
    }

    public Task<bool> CompleteTransferAsync(Guid transferId, string checksum)
    {
        if (!_transfers.TryGetValue(transferId, out var transfer))
            return Task.FromResult(false);

        transfer.Status = TransferStatus.Completed;
        transfer.BytesTransferred = transfer.FileSize;
        transfer.Checksum = checksum;
        transfer.CompletedAt = DateTimeOffset.UtcNow;

        _logger.LogInformation("File transfer completed: {TransferId} checksum={Checksum}", transferId, checksum);
        return Task.FromResult(true);
    }

    public Task<bool> FailTransferAsync(Guid transferId, string error)
    {
        if (!_transfers.TryGetValue(transferId, out var transfer))
            return Task.FromResult(false);

        transfer.Status = TransferStatus.Failed;
        transfer.CompletedAt = DateTimeOffset.UtcNow;

        _logger.LogError("File transfer failed: {TransferId} error={Error}", transferId, error);
        return Task.FromResult(true);
    }

    public Task<bool> CancelTransferAsync(Guid transferId)
    {
        if (!_transfers.TryGetValue(transferId, out var transfer))
            return Task.FromResult(false);

        transfer.Status = TransferStatus.Cancelled;
        transfer.CompletedAt = DateTimeOffset.UtcNow;

        _logger.LogInformation("File transfer cancelled: {TransferId}", transferId);
        return Task.FromResult(true);
    }

    public async Task<string> ComputeChecksumAsync(Stream stream)
    {
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexStringLower(hash);
    }

    public Task<IReadOnlyList<byte[]>> ChunkFileAsync(Stream fileStream, int chunkSize = 81920)
    {
        var chunks = new List<byte[]>();
        var buffer = new byte[chunkSize];

        int bytesRead;
        while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
        {
            var chunk = new byte[bytesRead];
            Array.Copy(buffer, chunk, bytesRead);
            chunks.Add(chunk);
        }

        return Task.FromResult<IReadOnlyList<byte[]>>(chunks.AsReadOnly());
    }

    public Task<bool> ValidateFileTransferAsync(string fileName, long fileSize)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return Task.FromResult(false);

        var extension = Path.GetExtension(fileName);
        if (BlockedExtensions.Contains(extension))
            return Task.FromResult(false);

        var maxBytes = _options.MaxFileTransferSizeMB * 1024L * 1024L;
        if (fileSize > maxBytes)
            return Task.FromResult(false);

        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return Task.FromResult(false);

        return Task.FromResult(true);
    }
}
