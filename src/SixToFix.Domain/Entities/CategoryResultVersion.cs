namespace SixToFix.Domain.Entities;

/// <summary>
/// Append-only. Rows are never updated or deleted.
/// Each reviewer action creates a new version row.
/// See ADR for immutable publish semantics.
/// </summary>
public class CategoryResultVersion
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CategoryResultId { get; set; }
    public int Version { get; set; }
    public int ActivityScore { get; set; }
    public string? ReviewNotes { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public CategoryResult CategoryResult { get; set; } = null!;
}
