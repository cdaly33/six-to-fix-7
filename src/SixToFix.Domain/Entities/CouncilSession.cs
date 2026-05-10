namespace SixToFix.Domain.Entities;

public class CouncilSession
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SkillRunId { get; set; }
    public string Status { get; set; } = "pending";
    public string? AdvocateOutputJson { get; set; }
    public string? SkepticOutputJson { get; set; }
    public string? JudgeOutputJson { get; set; }
    public string Decision { get; set; } = string.Empty;
    public decimal? AdjustedScore { get; set; }
    public string? Rationale { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public SkillRun SkillRun { get; set; } = null!;
}
