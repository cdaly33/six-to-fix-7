namespace SixToFix.Domain.Entities;

public class PolicyFlag
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SkillRunId { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public SkillRun SkillRun { get; set; } = null!;
}
