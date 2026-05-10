namespace SixToFix.Domain.Entities;

public class TelemetryEvent
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AuditRunId { get; set; }
    public int SkillRunCount { get; set; }
    public int PolicyTriggerCount { get; set; }
    public int CouncilRunCount { get; set; }
    public int ReviewerActionCount { get; set; }
    public int TotalTokensUsed { get; set; }
    public int TotalLatencyMs { get; set; }
    public DateTimeOffset InitializedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public AuditRun AuditRun { get; set; } = null!;
}
