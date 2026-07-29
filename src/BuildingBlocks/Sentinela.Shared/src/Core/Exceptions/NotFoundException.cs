namespace Sentinela.Shared.Core.Exceptions;

public class NotFoundException : DomainException
{
    public string EntityName { get; }
    public object Id { get; }

    public NotFoundException(string entityName, object id)
        : base($"Entity \"{entityName}\" with key ({id}) was not found.", "NOT_FOUND")
    {
        EntityName = entityName;
        Id = id;
    }
}
