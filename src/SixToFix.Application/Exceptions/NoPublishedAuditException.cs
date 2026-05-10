namespace SixToFix.Application.Exceptions;

public sealed class NoPublishedAuditException : Exception
{
    public string ClientSlug { get; }

    public NoPublishedAuditException(string clientSlug)
        : base($"No published audit found for client '{clientSlug}'.")
    {
        ClientSlug = clientSlug;
    }
}
