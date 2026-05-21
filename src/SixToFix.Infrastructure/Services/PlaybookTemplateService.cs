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
    private static readonly IReadOnlyList<TemplateSeed> DefaultTemplateSeeds =
    [
        new("Brand Messaging Matrix", Pillar.Brand, "doc", 92, "Define audience-specific value props and proof points.", """
# Brand Messaging Matrix

## Audience
- Segment:
- Core pain:

## Message pillars
1. Outcome promise
2. Differentiator
3. Proof
"""),
        new("Customer Journey Mapper", Pillar.Customer, "sheet", 88, "Map lifecycle stages, friction, and ownership.", """
# Customer Journey Mapper

| Stage | Customer Goal | Friction | Owner |
|---|---|---|---|
| Awareness |  |  |  |
| Consideration |  |  |  |
| Adoption |  |  |  |
"""),
        new("Service Packaging Worksheet", Pillar.Offering, "doc", 86, "Turn custom services into clear tiers.", """
# Service Packaging Worksheet

## Tier name
- Target client:
- Included outcomes:
- Delivery scope:
- Price:
"""),
        new("Campaign Brief Template", Pillar.Communication, "doc", 84, "Coordinate objectives, channels, and assets.", """
# Campaign Brief

- Goal:
- Audience:
- Core message:
- Channels:
- CTA:
"""),
        new("Pipeline Review Pack", Pillar.Sales, "sheet", 90, "Weekly pipeline inspection with stage-level blockers.", """
# Pipeline Review Pack

| Opportunity | Stage | Value | Next step | Blocker |
|---|---|---|---|---|
"""),
        new("KPI Accountability Scorecard", Pillar.Management, "sheet", 87, "Track pillar owners, targets, and trend direction.", """
# KPI Accountability Scorecard

| KPI | Owner | Target | Current | Trend |
|---|---|---|---|---|
""")
    ];

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
        var results = await QueryPublishedAsync(tenantId, pillar, ct);
        if (results.Count > 0) return results;

        await EnsureDefaultPublishedTemplatesAsync(tenantId, ct);
        return await QueryPublishedAsync(tenantId, pillar, ct);
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

    private async Task<List<PlaybookTemplate>> QueryPublishedAsync(Guid tenantId, Pillar? pillar, CancellationToken ct)
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

    private async Task EnsureDefaultPublishedTemplatesAsync(Guid tenantId, CancellationToken ct)
    {
        var hasPublished = await _db.PlaybookTemplates
            .AnyAsync(e => e.TenantId == tenantId && e.Status == PlaybookTemplateStatus.Published, ct);
        if (hasPublished) return;

        var now = DateTimeOffset.UtcNow;
        foreach (var seed in DefaultTemplateSeeds)
        {
            _db.PlaybookTemplates.Add(new PlaybookTemplate
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Pillar = seed.Pillar,
                Name = seed.Name,
                Format = seed.Format,
                Status = PlaybookTemplateStatus.Published,
                Popularity = seed.Popularity,
                LastUpdatedAt = now,
                Notes = seed.Notes,
                ContentMarkdown = seed.ContentMarkdown
            });
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Seeded {Count} default published templates for tenant {TenantId}",
            DefaultTemplateSeeds.Count, tenantId);
    }

    private sealed record TemplateSeed(
        string Name,
        Pillar? Pillar,
        string Format,
        int Popularity,
        string? Notes,
        string ContentMarkdown);
}
