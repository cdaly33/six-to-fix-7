namespace SixToFix.Domain.Entities;

/// <summary>
/// Tracks reviewer rejection counts per category per audit run.
/// 3 rejections within 24h triggers HTTP 409 REVIEWER_REJECTION_LOCKOUT.
/// Uses serializable transaction + advisory lock to prevent race conditions.
/// </summary>
public class ReviewerLockout
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AuditRunId { get; set; }
    public string Category { get; set; } = string.Empty;
    public Guid ReviewerUserId { get; set; }
    public int RejectionCount { get; set; }
    public DateTimeOffset WindowStartedAt { get; set; }
    public bool IsLocked { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public AuditRun AuditRun { get; set; } = null!;
}
