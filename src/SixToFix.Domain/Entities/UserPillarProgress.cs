using SixToFix.Domain.Enums;

namespace SixToFix.Domain.Entities;

public sealed class UserPillarProgress
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Pillar Pillar { get; set; }

    /// <summary>Progress percentage: 0–100.</summary>
    public int PercentComplete { get; set; }

    public DateTimeOffset LastActivityAt { get; set; }
}
