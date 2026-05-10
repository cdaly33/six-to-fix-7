namespace SixToFix.Domain.Entities;

public class AuditRun
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AuditId { get; set; }
    public string Status { get; set; } = "pending";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int? CompositeScore { get; set; }
    public decimal? SystemsMaturityScore { get; set; }
    public decimal? AiReadinessScore { get; set; }
    public string? Tier { get; set; }
    public Guid InitiatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Audit Audit { get; set; } = null!;
    public ICollection<SkillRun> SkillRuns { get; set; } = [];
    public ICollection<CategoryResult> CategoryResults { get; set; } = [];
}
