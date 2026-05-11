using Dapper;
using SixToFix.Application.Data;
using SixToFix.Application.Exceptions;
using SixToFix.Application.Models;
using SixToFix.Application.Services;
using SixToFix.Infrastructure.Data;

namespace SixToFix.Infrastructure.Services;

public sealed class CalibrationTracker : ICalibrationTracker
{
    private readonly SixToFixDbContext _db;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ITenantContext _tenant;
    private readonly ILogger<CalibrationTracker> _logger;

    public CalibrationTracker(
        SixToFixDbContext db,
        IDbConnectionFactory connectionFactory,
        ITenantContext tenant,
        ILogger<CalibrationTracker> logger)
    {
        _db = db;
        _connectionFactory = connectionFactory;
        _tenant = tenant;
        _logger = logger;
    }

    public async Task<CalibrationDeltaModel> RecordDeltaAsync(
        Guid auditRunId,
        Guid categoryId,
        Guid reviewerId,
        decimal originalActivityScore,
        decimal adjustedActivityScore,
        string? originalDocumentedStrategy,
        string? adjustedDocumentedStrategy,
        string overrideReasonCode,
        string notes,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(overrideReasonCode))
            throw new MissingOverrideReasonException();

        if (string.IsNullOrWhiteSpace(notes))
            throw new MissingCalibrationNotesException();

        var delta = new CalibrationDelta
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            AuditRunId = auditRunId,
            CategoryId = categoryId.ToString(),
            ReviewerId = reviewerId,
            OriginalActivityScore = originalActivityScore,
            AdjustedActivityScore = adjustedActivityScore,
            OriginalDocumentedStrategy = originalDocumentedStrategy,
            AdjustedDocumentedStrategy = adjustedDocumentedStrategy,
            OverrideReasonCode = overrideReasonCode,
            Notes = notes,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.CalibrationDeltas.Add(delta);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "CalibrationDelta recorded for AuditRun {AuditRunId}, Category {CategoryId}, Reviewer {ReviewerId}",
            auditRunId, categoryId, reviewerId);

        return MapToModel(delta);
    }

    public async Task<IReadOnlyList<CalibrationDeltaModel>> GetCalibrationHistoryAsync(Guid clientId, CancellationToken ct = default)
    {
        var auditIds = await _db.Audits
            .Where(a => a.ClientId == clientId)
            .Select(a => a.Id)
            .ToListAsync(ct);

        var auditRunIds = await _db.AuditRuns
            .Where(r => auditIds.Contains(r.AuditId))
            .Select(r => r.Id)
            .ToListAsync(ct);

        var deltas = await _db.CalibrationDeltas
            .Where(d => auditRunIds.Contains(d.AuditRunId))
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

        return deltas.Select(MapToModel).ToList();
    }

    public async Task<IReadOnlyList<CalibrationDeltaModel>> GetDeltasForAuditRunAsync(Guid auditRunId, CancellationToken ct = default)
    {
        var deltas = await _db.CalibrationDeltas
            .Where(d => d.AuditRunId == auditRunId)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(ct);

        return deltas.Select(MapToModel).ToList();
    }

    public async Task<CalibrationSummary> GetCalibrationSummaryAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(ct);

        var fromDate = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var toDate = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));

        const string sql = """
            SELECT
                COUNT(*) AS TotalOverrides,
                COALESCE(AVG(ABS(adjusted_activity_score - original_activity_score)), 0) AS AverageDelta,
                override_reason_code AS OverrideReasonCode
            FROM calibration_deltas
            WHERE tenant_id = @TenantId
              AND created_at >= @From
              AND created_at <= @To
            GROUP BY override_reason_code
            ORDER BY COUNT(*) DESC
            """;

        var rows = (await connection.QueryAsync<CalibrationSummaryRow>(sql, new
        {
            TenantId = _tenant.TenantId,
            From = fromDate,
            To = toDate
        })).ToList();

        var totalOverrides = rows.Sum(r => r.TotalOverrides);
        var averageDelta = rows.Count > 0
            ? rows.Average(r => r.AverageDelta)
            : 0m;
        var topReasonCodes = rows.Take(3).Select(r => r.OverrideReasonCode).ToList();

        return new CalibrationSummary(totalOverrides, averageDelta, topReasonCodes);
    }

    private static CalibrationDeltaModel MapToModel(CalibrationDelta d) =>
        new(d.Id, d.AuditRunId, d.CategoryId, d.ReviewerId,
            d.OriginalActivityScore, d.AdjustedActivityScore,
            d.OriginalDocumentedStrategy, d.AdjustedDocumentedStrategy,
            d.OverrideReasonCode, d.Notes, d.CreatedAt);

    private sealed record CalibrationSummaryRow(int TotalOverrides, decimal AverageDelta, string OverrideReasonCode);
}
