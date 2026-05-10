namespace SixToFix.Application.Models;

public record ReviewerLockoutStatus(
    bool IsLockedOut,
    int RejectionCount,
    DateTimeOffset? LockoutExpiresAt);
