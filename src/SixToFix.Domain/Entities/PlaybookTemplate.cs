using SixToFix.Domain.Enums;

namespace SixToFix.Domain.Entities;

public sealed class PlaybookTemplate
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Null indicates this template spans all pillars.</summary>
    public Pillar? Pillar { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Format hint: "doc", "spreadsheet", "kit", etc.</summary>
    public string Format { get; set; } = string.Empty;

    public PlaybookTemplateStatus Status { get; set; }
    public int Popularity { get; set; }
    public DateTimeOffset LastUpdatedAt { get; set; }
    public string? Notes { get; set; }
    public string ContentMarkdown { get; set; } = string.Empty;
}
