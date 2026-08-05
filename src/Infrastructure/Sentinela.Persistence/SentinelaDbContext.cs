using MediatR;
using Microsoft.EntityFrameworkCore;
using Sentinela.Persistence.Configurations;
using Sentinela.Shared.Core.Entities;
using Sentinela.Shared.Core.Interfaces;
using Sentinela.Shared.Domain.Alerting;
using Sentinela.Shared.Domain.Audit;
using Sentinela.Shared.Domain.Automation;
using Sentinela.Shared.Domain.Monitoring;
using Sentinela.Shared.Domain.Security;
using Sentinela.Persistence.Models;

namespace Sentinela.Persistence;

public class SentinelaDbContext : DbContext, IUnitOfWork
{
    private readonly IMediator _mediator;
    private readonly IDateTime _dateTime;

    public SentinelaDbContext(DbContextOptions<SentinelaDbContext> options, IMediator mediator, IDateTime dateTime) : base(options)
    {
        _mediator = mediator;
        _dateTime = dateTime;
    }

    public DbSet<Computer> Computers => Set<Computer>();
    public DbSet<Heartbeat> Heartbeats => Set<Heartbeat>();
    public DbSet<TimelineEntry> TimelineEntries => Set<TimelineEntry>();
    public DbSet<ApplicationUsage> ApplicationUsages => Set<ApplicationUsage>();
    public DbSet<UsbEvent> UsbEvents => Set<UsbEvent>();
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();
    public DbSet<SecurityAlert> SecurityAlerts => Set<SecurityAlert>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<Workflow> Workflows => Set<Workflow>();
    public DbSet<AuditTrail> AuditTrails => Set<AuditTrail>();
    public DbSet<CorrelationRule> CorrelationRules => Set<CorrelationRule>();
    public DbSet<VulnerabilityEvent> VulnerabilityEvents => Set<VulnerabilityEvent>();
    public DbSet<ScreenCaptureRecord> ScreenCaptureRecords => Set<ScreenCaptureRecord>();
    public DbSet<Screenshot> Screenshots => Set<Screenshot>();
    public DbSet<SoftwareInventoryItem> SoftwareInventory => Set<SoftwareInventoryItem>();
    public DbSet<EndpointSecurityStatus> EndpointSecurityStatuses => Set<EndpointSecurityStatus>();
    public DbSet<RemoteSession> RemoteSessions => Set<RemoteSession>();
    public DbSet<ScreenCapture> ScreenCaptures => Set<ScreenCapture>();
    public DbSet<FileTransferRecord> FileTransfers => Set<FileTransferRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SentinelaDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    break;
                case EntityState.Modified:
                    entry.Entity.MarkAsUpdated();
                    break;
                case EntityState.Deleted:
                    entry.Entity.MarkAsDeleted();
                    entry.State = EntityState.Modified;
                    break;
            }
        }

        var domainEntities = ChangeTracker.Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var entity in domainEntities)
        {
            var events = entity.DomainEvents.ToArray();
            entity.ClearDomainEvents();
            foreach (var domainEvent in events)
            {
                await _mediator.Publish(domainEvent, cancellationToken);
            }
        }

        return result;
    }

    public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
    {
        await SaveChangesAsync(cancellationToken);
        return true;
    }
}
