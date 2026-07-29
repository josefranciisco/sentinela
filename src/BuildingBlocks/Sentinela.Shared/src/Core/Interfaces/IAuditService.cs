using Sentinela.Shared.Domain.Audit;

namespace Sentinela.Shared.Core.Interfaces;

public interface IAuditService
{
    Task LogAsync(AuditTrail auditEntry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditTrail>> GetAuditTrailAsync(string resource, string? resourceId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditTrail>> SearchAuditLogsAsync(string searchTerm, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken cancellationToken = default);
}
