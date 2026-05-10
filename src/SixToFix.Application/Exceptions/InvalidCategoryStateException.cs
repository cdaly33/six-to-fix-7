namespace SixToFix.Application.Exceptions;

public sealed class InvalidCategoryStateException : Exception
{
    public string CurrentState { get; }

    public InvalidCategoryStateException(string currentState)
        : base($"Category is in invalid state '{currentState}' for this operation.")
    {
        CurrentState = currentState;
    }
}
