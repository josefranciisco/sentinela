namespace Sentinela.MessageBus.Subscribers;

using Sentinela.Shared.Messaging.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public abstract class EventSubscriberBase<TEvent> : BackgroundService where TEvent : IEvent
{
    protected readonly ILogger Logger;

    protected EventSubscriberBase(ILogger logger)
    {
        Logger = logger;
    }

    protected abstract Task HandleEventAsync(TEvent @event, CancellationToken cancellationToken);

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
    }
}
