using Sentinela.Shared.Core.Interfaces;
using Sentinela.Shared.Messaging.Events;

namespace Sentinela.MessageBus.Publishers;

public class EventPublisher
{
    private readonly IEventBus _eventBus;

    public EventPublisher(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public Task PublishAgentEventAsync<T>(T @event) where T : class, IEvent
        => _eventBus.PublishAsync(@event);

    public Task PublishSecurityEventAsync<T>(T @event) where T : class, IEvent
        => _eventBus.PublishAsync(@event);

    public Task PublishAlertEventAsync<T>(T @event) where T : class, IEvent
        => _eventBus.PublishAsync(@event);

    public Task PublishAuditEventAsync<T>(T @event) where T : class, IEvent
        => _eventBus.PublishAsync(@event);
}
