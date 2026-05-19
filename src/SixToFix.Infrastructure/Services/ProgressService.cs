using SixToFix.Application.Services;
using SixToFix.Infrastructure.Data;

namespace SixToFix.Infrastructure.Services;

/// <summary>
/// EF Core implementation of IProgressService.
///
/// Tenant isolation: UserPillarProgress rows carry TenantId, and the global query filter
/// on DbContext ensures rows are always scoped to the resolved tenant. The service does NOT
/// accept a tenantId parameter because progress is keyed by userId; the active tenant is
/// sourced from the scoped ITenantContext (same as ClientService). Since a user belongs to
/// exactly one tenant, WHERE UserId == userId is effectively tenant-scoped.
///
/// Average calculation: the average of 6 pillars where missing = 0 is computed in-process
/// to avoid dialect-specific SQL for sparse data.
/// </summary>
public sealed class ProgressService : IProgressService
{
    private static readonly Pillar[] AllPillars =
        (Pillar[])Enum.GetValues(typeof(Pillar));

    private readonly SixToFixDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ProgressService> _logger;

    public ProgressService(SixToFixDbContext db, ITenantContext tenant, ILogger<ProgressService> logger)
    {
        _db = db;
        _tenant = tenant;
        _logger = logger;
    }

    public async Task<IReadOnlyList<UserPillarProgress>> GetForUserAsync(
        Guid userId, CancellationToken ct = default)
    {
        return await _db.UserPillarProgresses
            .Where(e => e.UserId == userId)
            .OrderBy(e => e.Pillar)
            .ToListAsync(ct);
    }

    public async Task<UserPillarProgress?> GetForUserPillarAsync(
        Guid userId, Pillar pillar, CancellationToken ct = default)
    {
        return await _db.UserPillarProgresses
            .FirstOrDefaultAsync(e => e.UserId == userId && e.Pillar == pillar, ct);
    }

    public async Task<UserPillarProgress> SetPercentAsync(
        Guid userId, Pillar pillar, int percent, CancellationToken ct = default)
    {
        var clamped = Math.Clamp(percent, 0, 100);
        var now = DateTimeOffset.UtcNow;

        var existing = await _db.UserPillarProgresses
            .FirstOrDefaultAsync(e => e.UserId == userId && e.Pillar == pillar, ct);

        if (existing is not null)
        {
            existing.PercentComplete = clamped;
            existing.LastActivityAt = now;
        }
        else
        {
            existing = new UserPillarProgress
            {
                Id = Guid.NewGuid(),
                TenantId = _tenant.TenantId,
                UserId = userId,
                Pillar = pillar,
                PercentComplete = clamped,
                LastActivityAt = now
            };
            _db.UserPillarProgresses.Add(existing);

            _logger.LogInformation(
                "Created progress row for user {UserId}, pillar {Pillar}",
                userId, pillar);
        }

        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<int> GetAverageForUserAsync(
        Guid userId, CancellationToken ct = default)
    {
        var rows = await GetForUserAsync(userId, ct);

        // Missing pillars count as 0; total pillars is always 6.
        var sum = rows.Sum(r => r.PercentComplete);
        return sum / AllPillars.Length;
    }
}
