using SixToFix.Application.Models;
using SixToFix.Application.Services;
using SixToFix.Infrastructure.Data;

namespace SixToFix.Infrastructure.Services;

public sealed class PolicyEngine : IPolicyEngine
{
    private readonly SixToFixDbContext _dbContext;

    public PolicyEngine(SixToFixDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PolicyEvaluationResult> EvaluateCategoryAsync(Guid auditRunId, string category, CancellationToken ct = default)
    {
        var categoryResult = await _dbContext.CategoryResults
            .Where(result => result.AuditRunId == auditRunId && result.Category == category)
            .OrderByDescending(result => result.UpdatedAt)
            .FirstOrDefaultAsync(ct);
        if (categoryResult is null)
        {
            return new PolicyEvaluationResult(false, Array.Empty<string>());
        }

        var categoryScores = await _dbContext.CategoryResults
            .Where(result => result.AuditRunId == auditRunId)
            .Select(result => result.ActivityScore)
            .ToListAsync(ct);

        var tenantMedian = categoryScores.Count == 0 ? 0m : categoryScores.Average(score => (decimal)score);
        var variance = categoryScores.Count <= 1
            ? 0m
            : categoryScores.Average(score => (decimal)Math.Pow(score - (double)tenantMedian, 2));
        var skillRun = await _dbContext.SkillRuns
            .Where(run => run.AuditRunId == auditRunId && run.Category == category)
            .OrderByDescending(run => run.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var payload = new CategoryResultPayload(
            categoryResult.Category,
            categoryResult.ActivityScore,
            skillRun?.ConfidenceScore ?? 0m,
            skillRun?.OutputBlobReference is null ? Array.Empty<string>() : new[] { skillRun.OutputBlobReference },
            null);
        var context = new PolicyEvaluationContext(tenantMedian, variance == 0m ? 0m : (decimal)Math.Sqrt((double)variance), auditRunId);
        var flags = EvaluateCategory(payload, context);

        if (skillRun is not null)
        {
            var existingFlags = await _dbContext.PolicyFlags
                .Where(flag => flag.SkillRunId == skillRun.Id)
                .ToListAsync(ct);
            if (existingFlags.Count > 0)
            {
                _dbContext.PolicyFlags.RemoveRange(existingFlags);
            }

            if (flags.Count > 0)
            {
                _dbContext.PolicyFlags.AddRange(flags.Select(flag => new PolicyFlag
                {
                    Id = Guid.NewGuid(),
                    TenantId = skillRun.TenantId,
                    SkillRunId = skillRun.Id,
                    RuleCode = flag.Rule,
                    Severity = flag.Severity,
                    Detail = flag.Detail,
                    CreatedAt = DateTimeOffset.UtcNow
                }));
                await _dbContext.SaveChangesAsync(ct);
            }
        }

        var summary = SummarizeFlags(flags);
        return new PolicyEvaluationResult(summary.RequiresEscalation, summary.TriggerRuleNames);
    }

    public IReadOnlyList<PolicyFlagModel> EvaluateCategory(CategoryResultPayload payload, PolicyEvaluationContext context)
    {
        var flags = new List<PolicyFlagModel>();

        EvaluateLowConfidence(payload, flags);
        EvaluateMissingEvidence(payload, flags);
        EvaluateBenchmarkOutlier(payload, context, flags);
        EvaluateInsufficientEvidence(payload, flags);
        EvaluateScoreStrategyMismatch(payload, flags);

        return flags.AsReadOnly();
    }

    public bool RequiresCouncilEscalation(IReadOnlyList<PolicyFlagModel> flags)
        => flags.Any(flag => string.Equals(flag.Severity, "Trigger", StringComparison.Ordinal));

    public PolicyEvaluationSummary SummarizeFlags(IReadOnlyList<PolicyFlagModel> flags)
    {
        var warningCount = flags.Count(flag => string.Equals(flag.Severity, "Warning", StringComparison.Ordinal));
        var triggerFlags = flags
            .Where(flag => string.Equals(flag.Severity, "Trigger", StringComparison.Ordinal))
            .Select(flag => flag.Rule)
            .ToList()
            .AsReadOnly();

        return new PolicyEvaluationSummary(
            TotalFlags: flags.Count,
            WarningCount: warningCount,
            TriggerCount: triggerFlags.Count,
            TriggerRuleNames: triggerFlags,
            RequiresEscalation: triggerFlags.Count > 0);
    }

    private static void EvaluateLowConfidence(CategoryResultPayload payload, ICollection<PolicyFlagModel> flags)
    {
        if (payload.Confidence < 0.6m)
        {
            flags.Add(new PolicyFlagModel("LOW_CONFIDENCE", "Trigger", null));
        }
    }

    private static void EvaluateMissingEvidence(CategoryResultPayload payload, ICollection<PolicyFlagModel> flags)
    {
        if (payload.Evidence.Count == 0)
        {
            flags.Add(new PolicyFlagModel("MISSING_EVIDENCE", "Warning", null));
        }
    }

    private static void EvaluateBenchmarkOutlier(
        CategoryResultPayload payload,
        PolicyEvaluationContext context,
        ICollection<PolicyFlagModel> flags)
    {
        var deviation = Math.Abs(payload.ActivityScore - context.TenantMedianScore);
        var threshold = 2 * context.TenantStdDev;
        if (deviation <= threshold)
        {
            return;
        }

        var sigmaValue = context.TenantStdDev == 0m
            ? 0m
            : Math.Round(deviation / context.TenantStdDev, 1, MidpointRounding.AwayFromZero);

        flags.Add(new PolicyFlagModel(
            "BENCHMARK_OUTLIER",
            "Trigger",
            $"Score {payload.ActivityScore} deviates {sigmaValue}σ from tenant median {context.TenantMedianScore}"));
    }

    private static void EvaluateInsufficientEvidence(CategoryResultPayload payload, ICollection<PolicyFlagModel> flags)
    {
        if (payload.Evidence.Count > 0 && payload.Evidence.Count < 2)
        {
            flags.Add(new PolicyFlagModel("INSUFFICIENT_EVIDENCE", "Warning", null));
        }
    }

    private static void EvaluateScoreStrategyMismatch(CategoryResultPayload payload, ICollection<PolicyFlagModel> flags)
    {
        if (payload.ActivityScore > 7 &&
            (payload.DocumentedStrategy is null || string.Equals(payload.DocumentedStrategy, "none", StringComparison.OrdinalIgnoreCase)))
        {
            flags.Add(new PolicyFlagModel("SCORE_STRATEGY_MISMATCH", "Trigger", null));
        }
    }
}
