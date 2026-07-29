using System.Collections.Concurrent;
using Sentinela.ScreenCapture.Interfaces;

namespace Sentinela.ScreenCapture.Security;

public class AuditService : IAuditService
{
    private readonly ConcurrentBag<AuditEntry> _entries = new();
    private readonly ILogger<AuditService> _logger;

    public AuditService(ILogger<AuditService> logger) => _logger = logger;

    public Task LogAsync(AuditEntry entry)
    {
        _entries.Add(entry);
        _logger.LogInformation(
            "AUDIT|{Admin}|{Computer}|{StationUser}|{Result}|{Reason}|CaptureMs={CaptureMs}|UploadMs={UploadMs}",
            entry.AdminName, entry.ComputerName, entry.StationUser,
            entry.Result, entry.Reason, entry.CaptureTimeMs, entry.UploadTimeMs);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEntry>> QueryAsync(string? computerId = null,
        DateTime? from = null, DateTime? to = null)
    {
        var query = _entries.AsEnumerable();
        if (computerId != null) query = query.Where(e => e.ComputerId == computerId);
        if (from.HasValue) query = query.Where(e => e.Timestamp >= from.Value);
        if (to.HasValue) query = query.Where(e => e.Timestamp <= to.Value);
        return Task.FromResult<IReadOnlyList<AuditEntry>>(query.OrderByDescending(e => e.Timestamp).ToList());
    }
}
