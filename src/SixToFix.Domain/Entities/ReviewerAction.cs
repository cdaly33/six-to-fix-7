namespace SixToFix.Domain.Entities;

public class ReviewerAction
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AuditRunId { get; set; }
    public string CategoryId { get; set; } = string.Empty;
    public Guid ReviewerId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public AuditRun AuditRun { get; set; } = null!;
}
