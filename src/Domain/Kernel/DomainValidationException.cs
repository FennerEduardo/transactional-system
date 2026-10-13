namespace transactionalsystem.Domain.Kernel;

public sealed class DomainValidationException : Exception
{
    public DomainValidationException(string message) : base(message) { }
}
