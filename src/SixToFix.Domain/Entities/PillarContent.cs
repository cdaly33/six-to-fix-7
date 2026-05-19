using SixToFix.Domain.Enums;

namespace SixToFix.Domain.Entities;

public sealed class PillarContent
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Pillar Pillar { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }

    /// <summary>
    /// JSONB column. Schema: { strategy: [{title, points[]}], execution: [string],
    /// templates: [string], examples: [string], metrics: [[label, value]] }
    /// </summary>
    public string BodyJson { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
