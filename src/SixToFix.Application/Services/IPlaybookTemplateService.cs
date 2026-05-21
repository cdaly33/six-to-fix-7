namespace SixToFix.Application.Services;

/// <summary>
/// Manages tenant-scoped playbook templates through their Draft → Published → Archived lifecycle.
/// All methods require tenantId; cross-tenant leakage is prevented at the query layer.
/// </summary>
public interface IPlaybookTemplateService
{
    /// <summary>
    /// Returns all templates visible to an admin. Passing null is reserved for authorized SuperAdmin callers.
    /// </summary>
    Task<IReadOnlyList<PlaybookTemplate>> GetAllAsync(Guid? tenantId, CancellationToken ct = default);

    /// <summary>
    /// Returns all Published templates for a tenant. When <paramref name="pillar"/> is provided,
    /// results are filtered to that pillar plus any null-pillar (cross-cutting) templates.
    /// When <paramref name="pillar"/> is null, returns all Published templates regardless of pillar.
    /// If no published templates exist yet, starter templates are seeded for that tenant.
    /// </summary>
    Task<IReadOnlyList<PlaybookTemplate>> GetPublishedAsync(Guid tenantId, Pillar? pillar, CancellationToken ct = default);

    /// <summary>Returns a template by id within the tenant, or null if not found.</summary>
    Task<PlaybookTemplate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>Creates a new template. Status is forced to Draft regardless of input.</summary>
    Task<PlaybookTemplate> CreateAsync(Guid tenantId, PlaybookTemplate template, CancellationToken ct = default);

    /// <summary>Updates a template's mutable fields. TenantId and Status are not changed here.</summary>
    Task<PlaybookTemplate> UpdateAsync(Guid tenantId, PlaybookTemplate template, CancellationToken ct = default, bool includeAllTenants = false);

    /// <summary>Transitions a template to Published status.</summary>
    Task<PlaybookTemplate> PublishAsync(Guid tenantId, Guid id, CancellationToken ct = default, bool includeAllTenants = false);

    /// <summary>Transitions a template to Draft status.</summary>
    Task<PlaybookTemplate> UnpublishAsync(Guid tenantId, Guid id, CancellationToken ct = default, bool includeAllTenants = false);

    /// <summary>Transitions a template to Archived status.</summary>
    Task<PlaybookTemplate> ArchiveAsync(Guid tenantId, Guid id, CancellationToken ct = default, bool includeAllTenants = false);
}
