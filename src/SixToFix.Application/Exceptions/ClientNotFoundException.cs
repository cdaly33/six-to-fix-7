namespace SixToFix.Application.Exceptions;

public sealed class ClientNotFoundException : Exception
{
    public Guid ClientId { get; }

    public ClientNotFoundException(Guid clientId)
        : base($"Client '{clientId}' was not found.")
    {
        ClientId = clientId;
    }
}
