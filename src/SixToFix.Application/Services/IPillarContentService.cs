namespace SixToFix.Application.Services;

/// <summary>
/// Manages per-tenant pillar strategy content.
/// All methods scope to tenantId — cross-tenant access is impossible by design.
/// </summary>
public interface IPillarContentService
{
    /// <summary>Returns the content for a single pillar, seeding starter content if missing.</summary>
    Task<PillarContent?> GetForTenantAsync(Guid tenantId, Pillar pillar, CancellationToken ct = default);

    /// <summary>
    /// Returns content for all 6 pillars, lazily seeding starter rows for any that are missing.
    /// The returned list always has exactly 6 items after seeding.
    /// </summary>
    Task<IReadOnlyList<PillarContent>> GetAllForTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Insert or update a pillar content row. Sets UpdatedAt on each call.</summary>
    Task<PillarContent> UpsertAsync(Guid tenantId, Pillar pillar, string bodyJson, Guid updatedByUserId, CancellationToken ct = default);
}
