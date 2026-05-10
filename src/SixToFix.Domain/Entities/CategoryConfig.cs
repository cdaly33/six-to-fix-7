namespace SixToFix.Domain.Entities;

public class CategoryConfig
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AuditId { get; set; }
    public string Category { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
    public string? CustomPromptOverride { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Audit Audit { get; set; } = null!;
}
