namespace Sentinela.Shared.Core.Exceptions;

public class DomainException : Exception
{
    public string Code { get; }

    public DomainException(string message, string code = "DOMAIN_ERROR", Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }
}
