namespace SixToFix.Domain.Entities;

public class Audit
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }
    public string Status { get; set; } = "draft";
    public string? Title { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Client Client { get; set; } = null!;
    public ICollection<AuditRun> AuditRuns { get; set; } = [];
    public ICollection<CategoryConfig> CategoryConfigs { get; set; } = [];
}
