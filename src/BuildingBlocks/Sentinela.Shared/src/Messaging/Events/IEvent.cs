namespace Sentinela.Shared.Messaging.Events;

public interface IEvent
{
    Guid Id { get; }
    DateTimeOffset Timestamp { get; }
    string EventType { get; }
    string Source { get; }
}
