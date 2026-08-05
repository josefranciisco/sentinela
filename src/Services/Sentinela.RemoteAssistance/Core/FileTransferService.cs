using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sentinela.Persistence;
using Sentinela.Persistence.Models;
using Sentinela.RemoteAssistance.Configuration;

namespace Sentinela.RemoteAssistance.Core;

public class FileTransferService : IFileTransferService
{
    private readonly SentinelaDbContext _dbContext;
    private readonly ILogger<FileTransferService> _logger;
    private readonly RemoteAssistanceOptions _options;

    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bat", ".cmd", ".ps1", ".vbs", ".scr", ".com", ".pif",
        ".reg", ".msi", ".msp", ".jar", ".wsf", ".wsh", ".sh", ".appref-ms"
    };

    public FileTransferService(SentinelaDbContext dbContext, IOptions<RemoteAssistanceOptions> options, ILogger<FileTransferService> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FileTransfer> CreateTransferAsync(Guid sessionId, TransferDirection direction, string fileName, long fileSize, string sourcePath, string destinationPath, string transferredBy)
    {
        if (!_options.Enabled)
            throw new InvalidOperationException("Remote assistance is disabled.");

        var maxBytes = _options.MaxFileTransferSizeMB * 1024L * 1024L;
        if (fileSize > maxBytes)
            throw new InvalidOperationException($"File size {fileSize} exceeds maximum allowed {maxBytes} bytes.");

        var record = new FileTransferRecord
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Direction = direction.ToString(),
            FileName = fileName,
            FileSize = fileSize,
            BytesTransferred = 0,
            Status = TransferStatus.Pending.ToString(),
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            StartedAt = DateTimeOffset.UtcNow,
            TransferredBy = transferredBy,
            IsEncrypted = true
        };

        _dbContext.FileTransfers.Add(record);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("File transfer created: {TransferId} file={FileName} size={FileSize} direction={Direction}",
            record.Id, fileName, fileSize, direction);

        return MapToDomain(record);
    }

    public async Task<FileTransfer?> GetTransferAsync(Guid transferId)
    {
        var record = await _dbContext.FileTransfers.FindAsync(transferId);
        return record is null ? null : MapToDomain(record);
    }

    public async Task<IEnumerable<FileTransfer>> GetTransfersBySessionAsync(Guid sessionId)
    {
        var records = await _dbContext.FileTransfers
            .Where(t => t.SessionId == sessionId)
            .OrderBy(t => t.StartedAt)
            .ToListAsync();

        return records.Select(MapToDomain);
    }

    public async Task<bool> UpdateTransferProgressAsync(Guid transferId, long bytesTransferred)
    {
        var record = await _dbContext.FileTransfers.FindAsync(transferId);
        if (record is null) return false;

        record.BytesTransferred = bytesTransferred;
        record.Status = bytesTransferred >= record.FileSize
            ? TransferStatus.Completed.ToString()
            : TransferStatus.Transferring.ToString();

        if (record.Status == TransferStatus.Completed.ToString())
            record.CompletedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CompleteTransferAsync(Guid transferId, string checksum)
    {
        var record = await _dbContext.FileTransfers.FindAsync(transferId);
        if (record is null) return false;

        record.Status = TransferStatus.Completed.ToString();
        record.BytesTransferred = record.FileSize;
        record.Checksum = checksum;
        record.CompletedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("File transfer completed: {TransferId} checksum={Checksum}", transferId, checksum);
        return true;
    }

    public async Task<bool> FailTransferAsync(Guid transferId, string error)
    {
        var record = await _dbContext.FileTransfers.FindAsync(transferId);
        if (record is null) return false;

        record.Status = TransferStatus.Failed.ToString();
        record.CompletedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogError("File transfer failed: {TransferId} error={Error}", transferId, error);
        return true;
    }

    public async Task<bool> CancelTransferAsync(Guid transferId)
    {
        var record = await _dbContext.FileTransfers.FindAsync(transferId);
        if (record is null) return false;

        record.Status = TransferStatus.Cancelled.ToString();
        record.CompletedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("File transfer cancelled: {TransferId}", transferId);
        return true;
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

    private static FileTransfer MapToDomain(FileTransferRecord record) => new()
    {
        Id = record.Id,
        SessionId = record.SessionId,
        Direction = Enum.Parse<TransferDirection>(record.Direction),
        FileName = record.FileName,
        FileSize = record.FileSize,
        BytesTransferred = record.BytesTransferred,
        Status = Enum.Parse<TransferStatus>(record.Status),
        SourcePath = record.SourcePath,
        DestinationPath = record.DestinationPath,
        Checksum = record.Checksum,
        StartedAt = record.StartedAt,
        CompletedAt = record.CompletedAt,
        TransferredBy = record.TransferredBy,
        IsEncrypted = record.IsEncrypted
    };
}
