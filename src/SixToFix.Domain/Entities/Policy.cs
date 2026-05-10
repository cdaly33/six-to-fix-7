namespace SixToFix.Domain.Entities;

public class Policy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
    public bool IsEnabled { get; set; } = true;
    public string? ConfigJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
