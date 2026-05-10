namespace SixToFix.Domain.Entities;

public class CategoryResult
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AuditRunId { get; set; }
    public string Category { get; set; } = string.Empty;
    public int ActivityScore { get; set; }
    public decimal? SystemsMaturityContribution { get; set; }
    public string Status { get; set; } = "pending";
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public AuditRun AuditRun { get; set; } = null!;
    public ICollection<CategoryResultVersion> Versions { get; set; } = [];
}
