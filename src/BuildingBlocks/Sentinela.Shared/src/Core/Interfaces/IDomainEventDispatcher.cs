using Sentinela.Shared.Core.Events;

namespace Sentinela.Shared.Core.Interfaces;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}
