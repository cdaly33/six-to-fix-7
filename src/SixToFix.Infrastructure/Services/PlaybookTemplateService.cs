using SixToFix.Application.Services;
using SixToFix.Infrastructure.Data;

namespace SixToFix.Infrastructure.Services;

/// <summary>
/// EF Core implementation of IPlaybookTemplateService.
///
/// Tenant isolation: every query includes an explicit e.TenantId == tenantId predicate
/// in addition to the global query filter, providing defence-in-depth.
///
/// Status lifecycle: Draft → Published → Archived. An archived template can be
/// re-published by calling PublishAsync again if needed (not blocked here).
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

    public async Task<IReadOnlyList<PlaybookTemplate>> GetPublishedAsync(
        Guid tenantId, Pillar? pillar, CancellationToken ct = default)
    {
        var query = _db.PlaybookTemplates
            .Where(e => e.TenantId == tenantId && e.Status == PlaybookTemplateStatus.Published);

        if (pillar is not null)
        {
            // Include templates for the specific pillar AND cross-cutting (null pillar) ones.
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

        _db.PlaybookTemplates.Add(template);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created playbook template {TemplateId} ('{Name}') for tenant {TenantId}",
            template.Id, template.Name, tenantId);

        return template;
    }

    public async Task<PlaybookTemplate> UpdateAsync(
        Guid tenantId, PlaybookTemplate template, CancellationToken ct = default)
    {
        var existing = await RequireTemplateAsync(tenantId, template.Id, ct);

        existing.Name = template.Name;
        existing.Format = template.Format;
        existing.Pillar = template.Pillar;
        existing.Popularity = template.Popularity;
        existing.Notes = template.Notes;
        existing.LastUpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<PlaybookTemplate> PublishAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var template = await RequireTemplateAsync(tenantId, id, ct);
        template.Status = PlaybookTemplateStatus.Published;
        template.LastUpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Published playbook template {TemplateId} for tenant {TenantId}", id, tenantId);
        return template;
    }

    public async Task<PlaybookTemplate> ArchiveAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var template = await RequireTemplateAsync(tenantId, id, ct);
        template.Status = PlaybookTemplateStatus.Archived;
        template.LastUpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Archived playbook template {TemplateId} for tenant {TenantId}", id, tenantId);
        return template;
    }

    private async Task<PlaybookTemplate> RequireTemplateAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        return await _db.PlaybookTemplates
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == id, ct)
            ?? throw new InvalidOperationException(
                $"PlaybookTemplate {id} not found for tenant {tenantId}.");
    }
}
