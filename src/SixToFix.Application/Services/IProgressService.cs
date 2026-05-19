namespace SixToFix.Application.Services;

/// <summary>
/// Tracks per-user pillar progress percentages.
/// Progress rows are tenant-scoped; a user belongs to one tenant.
/// </summary>
public interface IProgressService
{
    /// <summary>Returns all progress rows for a user (0–6 rows).</summary>
    Task<IReadOnlyList<UserPillarProgress>> GetForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Returns the progress row for a specific pillar, or null if not started.</summary>
    Task<UserPillarProgress?> GetForUserPillarAsync(Guid userId, Pillar pillar, CancellationToken ct = default);

    /// <summary>
    /// Sets the completion percentage, clamped 0–100.
    /// Inserts a new row if one does not exist; updates LastActivityAt on every call.
    /// </summary>
    Task<UserPillarProgress> SetPercentAsync(Guid userId, Pillar pillar, int percent, CancellationToken ct = default);

    /// <summary>
    /// Returns the average percentage across all 6 pillars (missing pillars count as 0).
    /// Result is clamped to the range 0–100.
    /// </summary>
    Task<int> GetAverageForUserAsync(Guid userId, CancellationToken ct = default);
}
