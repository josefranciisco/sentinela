using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Sentinela.Shared.Core.Interfaces;
using Sentinela.Shared.Domain.Audit;

namespace Sentinela.Persistence.Interceptors;

public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUser _currentUser;
    private readonly IDateTime _dateTime;

    public AuditInterceptor(ICurrentUser currentUser, IDateTime dateTime)
    {
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context is null)
            return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var auditEntries = new List<AuditTrail>();

        foreach (var entry in context.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.Entity is AuditTrail)
                continue;

            var idProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Id");
            var auditEntry = new AuditTrail(
                userId: _currentUser.UserId,
                username: _currentUser.Username,
                action: entry.State.ToString(),
                resource: entry.Entity.GetType().Name,
                resourceId: idProperty?.CurrentValue?.ToString(),
                details: SerializeChanges(entry),
                ipAddress: _currentUser.IpAddress);

            if (_currentUser.TenantId != Guid.Empty)
            {
                auditEntry.TenantId = _currentUser.TenantId;
            }

            auditEntries.Add(auditEntry);
        }

        if (auditEntries.Count > 0)
        {
            context.Set<AuditTrail>().AddRange(auditEntries);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static string SerializeChanges(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        var changes = new Dictionary<string, object>();

        foreach (var property in entry.Properties)
        {
            if (!property.IsModified && entry.State != EntityState.Added)
                continue;

            if (property.Metadata.Name is "UpdatedAt" or "CreatedAt" or "IsDeleted")
                continue;

            changes[property.Metadata.Name] = new
            {
                Original = entry.State == EntityState.Added ? null : property.OriginalValue,
                Current = property.CurrentValue
            };
        }

        return changes.Count > 0
            ? JsonSerializer.Serialize(changes, new JsonSerializerOptions { WriteIndented = false })
            : string.Empty;
    }
}
