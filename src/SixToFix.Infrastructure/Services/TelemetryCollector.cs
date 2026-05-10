using SixToFix.Application.Exceptions;
using SixToFix.Application.Models;
using SixToFix.Application.Services;
using SixToFix.Infrastructure.Data;

namespace SixToFix.Infrastructure.Services;

public sealed class TelemetryCollector : ITelemetryCollector
{
    private readonly SixToFixDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<TelemetryCollector> _logger;

    public TelemetryCollector(SixToFixDbContext db, ITenantContext tenant, ILogger<TelemetryCollector> logger)
    {
        _db = db;
        _tenant = tenant;
        _logger = logger;
    }

    public async Task InitializeTelemetryAsync(Guid auditRunId, CancellationToken ct = default)
    {
        var exists = await _db.TelemetryEvents
            .AnyAsync(e => e.AuditRunId == auditRunId, ct);

        if (exists)
            throw new TelemetryAlreadyInitializedException(auditRunId);

        var telemetry = new TelemetryEvent
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            AuditRunId = auditRunId,
            InitializedAt = DateTimeOffset.UtcNow
        };

        _db.TelemetryEvents.Add(telemetry);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Telemetry initialized for AuditRun {AuditRunId}", auditRunId);
    }

    public async Task IncrementSkillRunCountAsync(Guid auditRunId, int tokensUsed, int latencyMs, CancellationToken ct = default)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE telemetry_events SET skill_run_count = skill_run_count + 1, total_tokens_used = total_tokens_used + {tokensUsed}, total_latency_ms = total_latency_ms + {latencyMs} WHERE audit_run_id = {auditRunId}",
            ct);

        _logger.LogInformation("SkillRun incremented for AuditRun {AuditRunId}, Tokens={Tokens}, Latency={Latency}ms", auditRunId, tokensUsed, latencyMs);
    }

    public async Task IncrementPolicyTriggerCountAsync(Guid auditRunId, CancellationToken ct = default)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE telemetry_events SET policy_trigger_count = policy_trigger_count + 1 WHERE audit_run_id = {auditRunId}",
            ct);
    }

    public async Task IncrementCouncilRunCountAsync(Guid auditRunId, CancellationToken ct = default)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE telemetry_events SET council_run_count = council_run_count + 1 WHERE audit_run_id = {auditRunId}",
            ct);
    }

    public async Task IncrementReviewerActionCountAsync(Guid auditRunId, CancellationToken ct = default)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE telemetry_events SET reviewer_action_count = reviewer_action_count + 1 WHERE audit_run_id = {auditRunId}",
            ct);
    }

    public async Task FinalizeTelemetryAsync(Guid auditRunId, CancellationToken ct = default)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE telemetry_events SET completed_at = now() WHERE audit_run_id = {auditRunId} AND completed_at IS NULL",
            ct);

        _logger.LogInformation("Telemetry finalized for AuditRun {AuditRunId}", auditRunId);
    }

    public async Task<IReadOnlyList<TelemetryEventModel>> GetDailyMetricsAsync(DateOnly date, CancellationToken ct = default)
    {
        var startOfDay = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var endOfDay = startOfDay.AddDays(1);

        var events = await _db.TelemetryEvents
            .IgnoreQueryFilters()
            .Where(e => e.InitializedAt >= startOfDay && e.InitializedAt < endOfDay)
            .ToListAsync(ct);

        return events.Select(e => new TelemetryEventModel(
            e.AuditRunId,
            e.SkillRunCount,
            e.PolicyTriggerCount,
            e.CouncilRunCount,
            e.ReviewerActionCount,
            e.TotalTokensUsed,
            e.TotalLatencyMs,
            e.InitializedAt,
            e.CompletedAt)).ToList();
    }
}
