using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using SixToFix.Application.Exceptions;
using SixToFix.Application.Hubs;
using SixToFix.Application.Models;
using SixToFix.Application.Services;
using SixToFix.Infrastructure.Data;
using SixToFix.Infrastructure.Hubs;

namespace SixToFix.Infrastructure.Services;

public sealed class AuditOrchestrator : IAuditOrchestrator
{
    private static readonly string[] SkillChain =
    [
        "6tofix-scorecard-rubric",
        "systems-maturity-scoring",
        "gap-analysis-template",
        "value-driver-rating",
        "derive-tier"
    ];

    private readonly ISkillRunner _skillRunner;
    private readonly IPolicyEngine _policyEngine;
    private readonly ICouncilRunner _councilRunner;
    private readonly ITelemetryCollector _telemetryCollector;
    private readonly IHubContext<AuditRunHub, IAuditRunHubClient> _hubContext;
    private readonly ILogger<AuditOrchestrator> _logger;
    private readonly SixToFixDbContext _db;
    private readonly ITenantContext _tenant;

    public AuditOrchestrator(
        ISkillRunner skillRunner,
        IPolicyEngine policyEngine,
        ICouncilRunner councilRunner,
        ITelemetryCollector telemetryCollector,
        IHubContext<AuditRunHub, IAuditRunHubClient> hubContext,
        ILogger<AuditOrchestrator> logger,
        SixToFixDbContext db,
        ITenantContext tenant)
    {
        _skillRunner = skillRunner;
        _policyEngine = policyEngine;
        _councilRunner = councilRunner;
        _telemetryCollector = telemetryCollector;
        _hubContext = hubContext;
        _logger = logger;
        _db = db;
        _tenant = tenant;
    }

    public async Task<AuditRun> CreateAuditRunAsync(Guid clientId, Guid createdByUserId, CancellationToken ct = default)
    {
        _ = await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId, ct)
            ?? throw new ClientNotFoundException(clientId);

        var audit = await _db.Audits
            .Where(a => a.ClientId == clientId)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct)
            ?? throw new ClientNotFoundException(clientId);

        var activeRun = await _db.AuditRuns
            .AnyAsync(r => r.AuditId == audit.Id && (r.Status == "pending" || r.Status == "running"), ct);

        if (activeRun)
            throw new AuditRunConflictException(clientId);

        var now = DateTimeOffset.UtcNow;
        var auditRun = new AuditRun
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            AuditId = audit.Id,
            Status = "pending",
            InitiatedByUserId = createdByUserId,
            StartedAt = now,
            CreatedAt = now
        };

        _db.AuditRuns.Add(auditRun);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("AuditRun {AuditRunId} created for Client {ClientId}", auditRun.Id, clientId);

        return auditRun;
    }

    public async Task StartAuditRunAsync(Guid auditRunId, CancellationToken ct = default)
    {
        var auditRun = await _db.AuditRuns
            .FirstOrDefaultAsync(r => r.Id == auditRunId, ct)
            ?? throw new AuditRunNotFoundException(auditRunId);

        if (auditRun.Status != "pending")
            throw new InvalidAuditRunStateException(auditRun.Status, "pending");

        auditRun.Status = "running";
        await _db.SaveChangesAsync(ct);
        await _telemetryCollector.InitializeTelemetryAsync(auditRunId, ct);

        var groupKey = auditRunId.ToString("N");
        await SendHubEventAsync(groupKey, "run-started", new { auditRunId }, ct);

        try
        {
            foreach (var skillName in SkillChain)
            {
                await SendHubEventAsync(groupKey, "skill-started", new { auditRunId, skillName }, ct);

                SkillRunResult skillResult;
                using var inputPayload = JsonDocument.Parse("{}");
                try
                {
                    skillResult = await _skillRunner.ExecuteSkillAsync(auditRunId, skillName, inputPayload, ct);
                }
                catch (Exception ex) when (IsSkillFatalException(ex))
                {
                    await MarkAuditRunFailedAsync(auditRunId, ex.Message, ct);
                    throw;
                }

                await _telemetryCollector.IncrementSkillRunCountAsync(auditRunId, skillResult.TokensUsed, skillResult.LatencyMs, ct);
                await SendHubEventAsync(groupKey, "skill-completed", new { auditRunId, skillName }, ct);
            }

            var categoryResults = await _db.CategoryResults
                .Where(r => r.AuditRunId == auditRunId)
                .ToListAsync(ct);
            var policyContext = BuildPolicyEvaluationContext(categoryResults, auditRunId);

            foreach (var categoryResult in categoryResults)
            {
                var payload = await BuildCategoryResultPayloadAsync(categoryResult, ct);
                var flags = _policyEngine.EvaluateCategory(payload, policyContext);

                if (_policyEngine.RequiresCouncilEscalation(flags))
                {
                    await _telemetryCollector.IncrementPolicyTriggerCountAsync(auditRunId, ct);
                    await _councilRunner.RunCouncilAsync(auditRunId, categoryResult.Id, flags, ct);
                    await _telemetryCollector.IncrementCouncilRunCountAsync(auditRunId, ct);
                }
            }

            auditRun.Status = "awaiting_review";
            auditRun.CompletedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            await _telemetryCollector.FinalizeTelemetryAsync(auditRunId, ct);

            await SendHubEventAsync(groupKey, "run-completed", new { auditRunId }, ct);
            _logger.LogInformation("AuditRun {AuditRunId} completed and awaiting review", auditRunId);
        }
        catch (Exception ex) when (ex is not (AuditRunNotFoundException or InvalidAuditRunStateException))
        {
            _logger.LogError(ex, "Unexpected failure in AuditRun {AuditRunId}", auditRunId);
            throw;
        }
    }

    public async Task<AuditRun> GetAuditRunAsync(Guid auditRunId, CancellationToken ct = default)
    {
        return await _db.AuditRuns
            .FirstOrDefaultAsync(r => r.Id == auditRunId, ct)
            ?? throw new AuditRunNotFoundException(auditRunId);
    }

    public async Task<IReadOnlyList<AuditRun>> GetAuditRunsForClientAsync(Guid clientId, CancellationToken ct = default)
    {
        var audits = await _db.Audits
            .Where(a => a.ClientId == clientId)
            .Select(a => a.Id)
            .ToListAsync(ct);

        return await _db.AuditRuns
            .Where(r => audits.Contains(r.AuditId))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task MarkAuditRunFailedAsync(Guid auditRunId, string failureReason, CancellationToken ct = default)
    {
        var auditRun = await _db.AuditRuns
            .FirstOrDefaultAsync(r => r.Id == auditRunId, ct);

        if (auditRun is null)
        {
            _logger.LogWarning("Attempted to mark non-existent AuditRun {AuditRunId} as failed", auditRunId);
            return;
        }

        auditRun.Status = "failed";
        auditRun.ErrorMessage = failureReason;
        auditRun.CompletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _telemetryCollector.FinalizeTelemetryAsync(auditRunId, ct);
        await SendHubEventAsync(auditRunId.ToString("N"), "run-failed", new { auditRunId, failureReason }, ct);

        _logger.LogError("AuditRun {AuditRunId} marked as failed: {FailureReason}", auditRunId, failureReason);
    }

    private async Task<CategoryResultPayload> BuildCategoryResultPayloadAsync(CategoryResult categoryResult, CancellationToken ct)
    {
        var confidence = await _db.SkillRuns
            .Where(run => run.AuditRunId == categoryResult.AuditRunId && run.Category == categoryResult.Category && run.ConfidenceScore.HasValue)
            .OrderByDescending(run => run.CompletedAt)
            .Select(run => run.ConfidenceScore)
            .FirstOrDefaultAsync(ct)
            ?? 1m;

        return new CategoryResultPayload(
            categoryResult.Category,
            categoryResult.ActivityScore,
            confidence,
            Array.Empty<string>(),
            null);
    }

    private static PolicyEvaluationContext BuildPolicyEvaluationContext(IReadOnlyList<CategoryResult> categoryResults, Guid auditRunId)
    {
        if (categoryResults.Count == 0)
        {
            return new PolicyEvaluationContext(0m, 0m, auditRunId);
        }

        var orderedScores = categoryResults
            .Select(result => (decimal)result.ActivityScore)
            .OrderBy(score => score)
            .ToList();
        var average = orderedScores.Average();
        var variance = orderedScores.Average(score => (double)((score - average) * (score - average)));
        var median = orderedScores.Count % 2 == 0
            ? (orderedScores[(orderedScores.Count / 2) - 1] + orderedScores[orderedScores.Count / 2]) / 2m
            : orderedScores[orderedScores.Count / 2];

        return new PolicyEvaluationContext(median, (decimal)Math.Sqrt(variance), auditRunId);
    }

    private async Task SendHubEventAsync(string groupKey, string eventType, object payload, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await _hubContext.Clients.Group(groupKey).ReceiveEvent(eventType, payload);
    }

    private static bool IsSkillFatalException(Exception ex)
    {
        var typeName = ex.GetType().Name;
        return typeName is "SkillSchemaValidationException" or "SkillCircuitOpenException";
    }
}
