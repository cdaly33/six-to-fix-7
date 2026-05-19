using SixToFix.Application.Services;
using SixToFix.Infrastructure.Data;

namespace SixToFix.Infrastructure.Services;

/// <summary>
/// EF Core implementation of IPillarContentService.
///
/// Tenant isolation: the SixToFixDbContext global query filter already restricts
/// PillarContent rows to the resolved tenant. The explicit WHERE e.TenantId == tenantId
/// clauses in every query provide defence-in-depth and make the contract clear at the call site,
/// matching the pattern used by ClientService.
///
/// Placeholder seeding: GetAllForTenantAsync lazily ensures all 6 Pillar rows exist
/// (body = {"placeholder":true}). This is the simplest path — no separate seeder service
/// or page-level call needed. The dashboard simply calls GetAllForTenantAsync once and always
/// gets back exactly 6 rows.
/// </summary>
public sealed class PillarContentService : IPillarContentService
{
    private static readonly Pillar[] AllPillars =
        (Pillar[])Enum.GetValues(typeof(Pillar));

    private const string PlaceholderBodyJson = """{"placeholder":true}""";
    private const string PlaceholderTitle = "Strategy Pillar";
    private const string PlaceholderSubtitle = "No content yet — your administrator can add strategy details here.";

    private readonly SixToFixDbContext _db;
    private readonly ILogger<PillarContentService> _logger;

    public PillarContentService(SixToFixDbContext db, ILogger<PillarContentService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PillarContent?> GetForTenantAsync(
        Guid tenantId, Pillar pillar, CancellationToken ct = default)
    {
        return await _db.PillarContents
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Pillar == pillar, ct);
    }

    /// <summary>
    /// Returns all 6 pillar content rows for the tenant.
    /// Any missing pillars are seeded on-demand with placeholder content before returning.
    /// </summary>
    public async Task<IReadOnlyList<PillarContent>> GetAllForTenantAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var existing = await _db.PillarContents
            .Where(e => e.TenantId == tenantId)
            .ToListAsync(ct);

        var missingPillars = AllPillars.Except(existing.Select(e => e.Pillar)).ToList();
        if (missingPillars.Count > 0)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var pillar in missingPillars)
            {
                var placeholder = new PillarContent
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Pillar = pillar,
                    Title = PlaceholderTitle,
                    Subtitle = PlaceholderSubtitle,
                    BodyJson = PlaceholderBodyJson,
                    CreatedAt = now,
                    UpdatedAt = now,
                    UpdatedByUserId = null
                };
                _db.PillarContents.Add(placeholder);
                existing.Add(placeholder);
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Seeded {Count} placeholder pillar content row(s) for tenant {TenantId}",
                missingPillars.Count, tenantId);
        }

        return existing.OrderBy(e => e.Pillar).ToList();
    }

    public async Task<PillarContent> UpsertAsync(
        Guid tenantId, Pillar pillar, string bodyJson, Guid updatedByUserId, CancellationToken ct = default)
    {
        var existing = await _db.PillarContents
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Pillar == pillar, ct);

        var now = DateTimeOffset.UtcNow;

        if (existing is not null)
        {
            existing.BodyJson = bodyJson;
            existing.UpdatedAt = now;
            existing.UpdatedByUserId = updatedByUserId;
        }
        else
        {
            existing = new PillarContent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Pillar = pillar,
                Title = PlaceholderTitle,
                Subtitle = PlaceholderSubtitle,
                BodyJson = bodyJson,
                CreatedAt = now,
                UpdatedAt = now,
                UpdatedByUserId = updatedByUserId
            };
            _db.PillarContents.Add(existing);
        }

        await _db.SaveChangesAsync(ct);
        return existing;
    }
}
