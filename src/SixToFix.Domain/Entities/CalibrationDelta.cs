namespace SixToFix.Domain.Entities;

public class CalibrationDelta
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AuditRunId { get; set; }
    public string CategoryId { get; set; } = string.Empty;
    public Guid ReviewerId { get; set; }
    public decimal OriginalActivityScore { get; set; }
    public decimal AdjustedActivityScore { get; set; }
    public string? OriginalDocumentedStrategy { get; set; }
    public string? AdjustedDocumentedStrategy { get; set; }
    public string OverrideReasonCode { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public AuditRun AuditRun { get; set; } = null!;
}
