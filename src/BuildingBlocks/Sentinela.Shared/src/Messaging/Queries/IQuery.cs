namespace Sentinela.Shared.Messaging.Queries;

public interface IQuery
{
    Guid Id { get; }
    DateTimeOffset Timestamp { get; }
    string QueryType { get; }
}
