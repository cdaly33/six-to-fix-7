namespace SixToFix.Application.Exceptions;

public sealed class MissingOverrideReasonException : Exception
{
    public MissingOverrideReasonException()
        : base("An override reason code is required for score adjustments.")
    {
    }
}
