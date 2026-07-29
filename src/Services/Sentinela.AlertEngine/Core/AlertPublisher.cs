using Sentinela.Shared.Core.Interfaces;
using Sentinela.Shared.Domain.Alerting;
using Sentinela.Shared.Domain.Monitoring.Enums;
using Sentinela.Shared.Domain.Security;
using Sentinela.Shared.Messaging.Events;

namespace Sentinela.AlertEngine.Core;

public interface IAlertPublisher
{
    Task PublishAlertAsync(SecurityAlert alert);
    Task PublishAlertBatchAsync(IEnumerable<SecurityAlert> alerts);
}

public class AlertPublisher : IAlertPublisher
{
    private readonly IEventBus _eventBus;
    private readonly IRepository<Alert> _alertRepo;
    private readonly ILogger<AlertPublisher> _logger;

    public AlertPublisher(IEventBus eventBus, IRepository<Alert> alertRepo, ILogger<AlertPublisher> logger)
    {
        _eventBus = eventBus;
        _alertRepo = alertRepo;
        _logger = logger;
    }

    public async Task PublishAlertAsync(SecurityAlert alert)
    {
        var alertEntity = new Alert(
            Guid.Empty,
            alert.ComputerId,
            alert.Title,
            alert.Description,
            alert.Severity,
            alert.Category,
            "AlertEngine");

        await _alertRepo.AddAsync(alertEntity);

        await _eventBus.PublishAsync(new AlertCreatedEvent
        {
            AlertId = alertEntity.Id,
            Title = alertEntity.Title,
            Severity = alertEntity.Severity,
            Category = alertEntity.Category,
            ComputerId = alertEntity.ComputerId,
            Timestamp = alertEntity.Timestamp
        });

        _logger.LogInformation("Alert published: {Title} [{Severity}]", alert.Title, alert.Severity);
    }

    public async Task PublishAlertBatchAsync(IEnumerable<SecurityAlert> alerts)
    {
        foreach (var alert in alerts)
        {
            await PublishAlertAsync(alert);
        }
    }
}

public class AlertCreatedEvent : IEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(AlertCreatedEvent);
    public string Source => "Sentinela.AlertEngine";

    public Guid AlertId { get; init; }
    public string Title { get; init; } = string.Empty;
    public Severity Severity { get; init; }
    public string Category { get; init; } = string.Empty;
    public Guid? ComputerId { get; init; }
}
