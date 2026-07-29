using MediatR;

namespace Sentinela.Shared.Core.Events;

public interface IDomainEvent : INotification
{
    DateTimeOffset Timestamp { get; }
}
