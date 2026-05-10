namespace SixToFix.Application.Exceptions;

public sealed class InvalidAuditRunStateException : Exception
{
    public string CurrentState { get; }
    public string RequiredState { get; }

    public InvalidAuditRunStateException(string currentState, string requiredState)
        : base($"AuditRun is in state '{currentState}' but '{requiredState}' is required.")
    {
        CurrentState = currentState;
        RequiredState = requiredState;
    }
}
