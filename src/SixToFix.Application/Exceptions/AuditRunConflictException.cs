namespace SixToFix.Application.Exceptions;

public sealed class AuditRunConflictException : Exception
{
    public Guid ClientId { get; }

    public AuditRunConflictException(Guid clientId)
        : base($"An active audit run already exists for client '{clientId}'.")
    {
        ClientId = clientId;
    }
}
