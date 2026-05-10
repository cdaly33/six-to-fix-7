namespace SixToFix.Application.Exceptions;

/// <summary>Maps to HTTP 502 — council deliberation failed fatally.</summary>
public sealed class CouncilExecutionException : Exception
{
    public CouncilExecutionException(string reason)
        : base($"Council execution failed: {reason}")
    {
    }
}
