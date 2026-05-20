using SixToFix.Application.Services;
using SixToFix.Infrastructure.Data;

namespace SixToFix.Infrastructure.Services;

/// <summary>
/// EF Core implementation of IPlaybookTemplateService.
///
/// Tenant isolation: every tenant-scoped query includes an explicit e.TenantId == tenantId
/// predicate in addition to the global query filter, providing defence-in-depth. The only
/// cross-tenant path is GetAllAsync(null), reserved for authorized SuperAdmin callers.
///
/// CreateAsync always forces Draft regardless of the caller-supplied Status value.
/// </summary>
public sealed class PlaybookTemplateService : IPlaybookTemplateService
{
    private readonly SixToFixDbContext _db;
    private readonly ILogger<PlaybookTemplateService> _logger;

    public PlaybookTemplateService(SixToFixDbContext db, ILogger<PlaybookTemplateService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PlaybookTemplate>> GetAllAsync(
        Guid? tenantId, CancellationToken ct = default)
    {
        var query = tenantId.HasValue
            ? _db.PlaybookTemplates.Where(e => e.TenantId == tenantId.Value)
            : _db.PlaybookTemplates.IgnoreQueryFilters();

        return await query
            .OrderByDescending(e => e.LastUpdatedAt)
            .ThenBy(e => e.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PlaybookTemplate>> GetPublishedAsync(
        Guid tenantId, Pillar? pillar, CancellationToken ct = default)
    {
        var query = _db.PlaybookTemplates
            .Where(e => e.TenantId == tenantId && e.Status == PlaybookTemplateStatus.Published);

        if (pillar is not null)
        {
            query = query.Where(e => e.Pillar == pillar || e.Pillar == null);
        }

        return await query
            .OrderByDescending(e => e.Popularity)
            .ThenBy(e => e.Name)
            .ToListAsync(ct);
    }

    public async Task<PlaybookTemplate?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.PlaybookTemplates
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == id, ct);
    }

    public async Task<PlaybookTemplate> CreateAsync(
        Guid tenantId, PlaybookTemplate template, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        template.Id = Guid.NewGuid();
        template.TenantId = tenantId;
        template.Status = PlaybookTemplateStatus.Draft;
        template.LastUpdatedAt = now;
        template.ContentMarkdown ??= string.Empty;

        _db.PlaybookTemplates.Add(template);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created playbook template {TemplateId} ('{Name}') for tenant {TenantId}",
            template.Id, template.Name, tenantId);

        return template;
    }

    public async Task<PlaybookTemplate> UpdateAsync(
        Guid tenantId, PlaybookTemplate template, CancellationToken ct = default, bool includeAllTenants = false)
    {
        var existing = await RequireTemplateAsync(tenantId, template.Id, includeAllTenants, ct);

        existing.Name = template.Name;
        existing.Format = template.Format;
        existing.Pillar = template.Pillar;
        existing.Popularity = template.Popularity;
        existing.Notes = template.Notes;
        existing.ContentMarkdown = template.ContentMarkdown;
        existing.LastUpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<PlaybookTemplate> PublishAsync(
        Guid tenantId, Guid id, CancellationToken ct = default, bool includeAllTenants = false)
    {
        var template = await RequireTemplateAsync(tenantId, id, includeAllTenants, ct);
        template.Status = PlaybookTemplateStatus.Published;
        template.LastUpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Published playbook template {TemplateId} for tenant {TenantId}", id, tenantId);
        return template;
    }

    public async Task<PlaybookTemplate> UnpublishAsync(
        Guid tenantId, Guid id, CancellationToken ct = default, bool includeAllTenants = false)
    {
        var template = await RequireTemplateAsync(tenantId, id, includeAllTenants, ct);
        template.Status = PlaybookTemplateStatus.Draft;
        template.LastUpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Unpublished playbook template {TemplateId} for tenant {TenantId}", id, tenantId);
        return template;
    }

    public async Task<PlaybookTemplate> ArchiveAsync(
        Guid tenantId, Guid id, CancellationToken ct = default, bool includeAllTenants = false)
    {
        var template = await RequireTemplateAsync(tenantId, id, includeAllTenants, ct);
        template.Status = PlaybookTemplateStatus.Archived;
        template.LastUpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Archived playbook template {TemplateId} for tenant {TenantId}", id, tenantId);
        return template;
    }

    private async Task<PlaybookTemplate> RequireTemplateAsync(
        Guid tenantId, Guid id, bool includeAllTenants, CancellationToken ct)
    {
        var query = includeAllTenants
            ? _db.PlaybookTemplates.IgnoreQueryFilters()
            : _db.PlaybookTemplates;

        return await query
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == id, ct)
            ?? throw new InvalidOperationException(
                $"PlaybookTemplate {id} not found for tenant {tenantId}.");
    }
}
