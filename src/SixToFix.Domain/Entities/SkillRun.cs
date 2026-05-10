namespace SixToFix.Domain.Entities;

public class SkillRun
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AuditRunId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int SequenceIndex { get; set; }
    public string Status { get; set; } = "pending";
    public string? InputBlobReference { get; set; }
    public string? OutputBlobReference { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public int? ActivityScore { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public AuditRun AuditRun { get; set; } = null!;
    public ICollection<PolicyFlag> PolicyFlags { get; set; } = [];
}
