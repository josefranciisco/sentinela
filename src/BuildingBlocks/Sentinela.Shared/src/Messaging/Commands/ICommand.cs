namespace Sentinela.Shared.Messaging.Commands;

public interface ICommand
{
    Guid Id { get; }
    DateTimeOffset Timestamp { get; }
    string CommandType { get; }
    string Source { get; }
}
