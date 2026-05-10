using SixToFix.Application.Models;

namespace SixToFix.Application.Exceptions;

public sealed class ReviewerLockoutException : Exception
{
    public ReviewerLockoutStatus LockoutStatus { get; }

    public ReviewerLockoutException(ReviewerLockoutStatus lockoutStatus)
        : base("REVIEWER_REJECTION_LOCKOUT")
    {
        LockoutStatus = lockoutStatus;
    }
}
