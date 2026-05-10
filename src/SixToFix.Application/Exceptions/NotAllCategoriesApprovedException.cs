namespace SixToFix.Application.Exceptions;

public sealed class NotAllCategoriesApprovedException : Exception
{
    public int ApprovedCount { get; }
    public int RequiredCount { get; }

    public NotAllCategoriesApprovedException(int approvedCount, int requiredCount)
        : base($"Only {approvedCount} of {requiredCount} required categories are approved.")
    {
        ApprovedCount = approvedCount;
        RequiredCount = requiredCount;
    }
}
