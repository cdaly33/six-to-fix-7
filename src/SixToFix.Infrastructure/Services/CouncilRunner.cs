using System.Text.Json;
using Polly.Registry;
using SixToFix.Application.Exceptions;
using SixToFix.Application.Models;
using SixToFix.Application.Services;
using SixToFix.Infrastructure.Data;

namespace SixToFix.Infrastructure.Services;

public sealed class CouncilRunner : ICouncilRunner
{
    private const string AdvocateSystemPrompt = "You are the Advocate in a marketing maturity scoring council. Your role is to make the strongest possible case for the client's current score, citing all available evidence. You see the category score, confidence level, and evidence items. Respond with a JSON object matching the AdvocatePosition schema.";
    private const string SkepticSystemPrompt = "You are the Skeptic in a marketing maturity scoring council. Your role is to challenge the Advocate's position and identify risks of overscoring. You see the category data and the Advocate's position. Respond with a JSON object matching the SkepticPosition schema.";
    private const string JudgeSystemPrompt = "You are the Method Judge in a marketing maturity scoring council. Your role is to arbitrate between the Advocate and Skeptic positions using objective methodology. You see the category data and both prior positions. Produce a definitive recommended score and verdict. Respond with a JSON object matching the MethodJudgePosition schema.";

    private const string AdvocateSchema = """{"type":"object","properties":{"category_id":{"type":"string","enum":["brand","customer","offering","communications","sales","management"]},"recommended_score":{"type":"integer","minimum":0,"maximum":10},"rationale":{"type":"string","minLength":20,"maxLength":1000},"supporting_evidence":{"type":"array","items":{"type":"string","minLength":5},"minItems":1},"confidence_in_position":{"type":"number","minimum":0.0,"maximum":1.0}},"required":["category_id","recommended_score","rationale","supporting_evidence"],"additionalProperties":false}""";
    private const string SkepticSchema = """{"type":"object","properties":{"category_id":{"type":"string","enum":["brand","customer","offering","communications","sales","management"]},"concerns":{"type":"array","items":{"type":"string","minLength":10,"maxLength":500},"minItems":1},"recommended_score":{"type":"integer","minimum":0,"maximum":10},"risk_assessment":{"type":"string","minLength":20,"maxLength":1000},"advocate_rebuttal":{"type":"string","maxLength":500}},"required":["category_id","concerns","recommended_score","risk_assessment"],"additionalProperties":false}""";
    private const string JudgeSchema = """{"type":"object","properties":{"category_id":{"type":"string","enum":["brand","customer","offering","communications","sales","management"]},"process_verdict":{"type":"string","enum":["compliant","non_compliant"]},"final_recommended_score":{"type":"integer","minimum":0,"maximum":10},"arbitration_notes":{"type":"string","minLength":20,"maxLength":1500},"methodology_issues":{"type":"array","items":{"type":"string"}}},"required":["category_id","process_verdict","final_recommended_score","arbitration_notes"],"additionalProperties":false}""";

    private readonly IAIClient _aiClient;
    private readonly SixToFixDbContext _dbContext;
    private readonly ResiliencePipelineProvider<string> _pipelineProvider;
    private readonly ILogger<CouncilRunner> _logger;

    public CouncilRunner(
        IAIClient aiClient,
        SixToFixDbContext dbContext,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<CouncilRunner> logger)
    {
        _aiClient = aiClient;
        _dbContext = dbContext;
        _pipelineProvider = pipelineProvider;
        _logger = logger;
    }

    public async Task<CouncilResult> RunCouncilAsync(Guid auditRunId, string category, CancellationToken ct = default)
    {
        var categoryResult = await _dbContext.CategoryResults
            .Where(result => result.AuditRunId == auditRunId && result.Category == category)
            .OrderByDescending(result => result.UpdatedAt)
            .FirstOrDefaultAsync(ct)
            ?? throw new CouncilExecutionException($"Category '{category}' was not found for audit run '{auditRunId}'.");

        var decision = await RunCouncilAsync(auditRunId, categoryResult.Id, Array.Empty<PolicyFlagModel>(), ct);
        return new CouncilResult(decision.DecisionType, decision.AdjustedScores);
    }

    public async Task<CouncilDecisionModel> RunCouncilAsync(
        Guid auditRunId,
        Guid categoryId,
        IReadOnlyList<PolicyFlagModel> triggeringFlags,
        CancellationToken ct = default)
    {
        var auditRun = await _dbContext.AuditRuns.FirstOrDefaultAsync(run => run.Id == auditRunId, ct)
            ?? throw new CouncilExecutionException($"Audit run '{auditRunId}' was not found.");
        var categoryResult = await _dbContext.CategoryResults
            .FirstOrDefaultAsync(result => result.Id == categoryId && result.AuditRunId == auditRunId, ct)
            ?? throw new CouncilExecutionException($"Category result '{categoryId}' was not found for audit run '{auditRunId}'.");
        var skillRun = await ResolveSkillRunAsync(auditRunId, categoryResult.Category, ct)
            ?? throw new CouncilExecutionException($"No skill run was found for category '{categoryResult.Category}'.");

        var startedAt = DateTimeOffset.UtcNow;

        var flagsPayload = triggeringFlags.Select(flag => new { rule = flag.Rule, severity = flag.Severity, detail = flag.Detail });
        var categoryPayload = new
        {
            category_id = categoryResult.Category,
            current_score = categoryResult.ActivityScore,
            triggering_flags = flagsPayload
        };
        var basePrompt = JsonSerializer.Serialize(categoryPayload);

        string? advocateJson = null;
        string? skepticJson = null;
        string? judgeJson = null;
        decimal overallConfidence = 0m;
        string decisionType = "confirmed";
        var adjustedScores = new Dictionary<string, int>(StringComparer.Ordinal);
        var rationale = "Council confirmed the original category score.";
        var sessionStatus = "completed";

        try
        {
            advocateJson = await ExecutePersonaAsync("council-advocate", AdvocateSystemPrompt, basePrompt, AdvocateSchema, ct);
            overallConfidence = ReadDecimal(advocateJson, "confidence_in_position") ?? 0.8m;

            var skepticPrompt = $"{basePrompt}\nAdvocate position: {advocateJson}";
            skepticJson = await ExecutePersonaAsync("council-skeptic", SkepticSystemPrompt, skepticPrompt, SkepticSchema, ct);

            var judgePrompt = $"{basePrompt}\nAdvocate position: {advocateJson}\nSkeptic position: {skepticJson}";
            judgeJson = await ExecutePersonaAsync("council-judge", JudgeSystemPrompt, judgePrompt, JudgeSchema, ct);

            using var judgeDocument = JsonDocument.Parse(judgeJson);
            var finalScore = judgeDocument.RootElement.GetProperty("final_recommended_score").GetInt32();
            rationale = judgeDocument.RootElement.GetProperty("arbitration_notes").GetString() ?? rationale;
            if (finalScore != categoryResult.ActivityScore)
            {
                decisionType = "adjusted";
                adjustedScores[categoryResult.Category] = finalScore;
            }
        }
        catch (Exception ex)
        {
            sessionStatus = "failed";
            rationale = "Council deliberation failed; retaining the original category score.";
            _logger.LogError(ex, "Council deliberation failed for audit run {AuditRunId} and category {CategoryId}", auditRunId, categoryId);
        }

        var completedAt = DateTimeOffset.UtcNow;
        var session = new CouncilSession
        {
            Id = Guid.NewGuid(),
            TenantId = auditRun.TenantId,
            SkillRunId = skillRun.Id,
            Status = sessionStatus,
            AdvocateOutputJson = advocateJson,
            SkepticOutputJson = skepticJson,
            JudgeOutputJson = judgeJson,
            Decision = decisionType,
            AdjustedScore = adjustedScores.TryGetValue(categoryResult.Category, out var adjustedScore)
                ? adjustedScore
                : null,
            Rationale = rationale,
            CreatedAt = startedAt,
            CompletedAt = completedAt
        };

        _dbContext.CouncilSessions.Add(session);
        await _dbContext.SaveChangesAsync(ct);

        return new CouncilDecisionModel(
            auditRunId,
            decisionType,
            new Dictionary<string, int>(adjustedScores),
            overallConfidence,
            rationale,
            completedAt);
    }

    public async Task<CouncilDecisionModel?> GetCouncilDecisionAsync(Guid auditRunId, Guid categoryId, CancellationToken ct = default)
    {
        var categoryResult = await _dbContext.CategoryResults
            .FirstOrDefaultAsync(result => result.Id == categoryId && result.AuditRunId == auditRunId, ct);
        if (categoryResult is null)
        {
            return null;
        }

        var skillRun = await ResolveSkillRunAsync(auditRunId, categoryResult.Category, ct);
        if (skillRun is null)
        {
            return null;
        }

        var session = await _dbContext.CouncilSessions
            .Where(item => item.SkillRunId == skillRun.Id)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (session is null)
        {
            return null;
        }

        var adjustedScores = new Dictionary<string, int>(StringComparer.Ordinal);
        if (session.AdjustedScore.HasValue)
        {
            adjustedScores[categoryResult.Category] = decimal.ToInt32(session.AdjustedScore.Value);
        }

        return new CouncilDecisionModel(
            auditRunId,
            session.Decision,
            adjustedScores,
            session.AdjustedScore ?? 0m,
            session.Rationale ?? string.Empty,
            session.CompletedAt ?? session.CreatedAt);
    }

    private async Task<SkillRun?> ResolveSkillRunAsync(Guid auditRunId, string category, CancellationToken ct)
    {
        return await _dbContext.SkillRuns
            .Where(run => run.AuditRunId == auditRunId && run.Category == category)
            .OrderByDescending(run => run.CreatedAt)
            .FirstOrDefaultAsync(ct)
            ?? await _dbContext.SkillRuns
                .Where(run => run.AuditRunId == auditRunId)
                .OrderByDescending(run => run.CreatedAt)
                .FirstOrDefaultAsync(ct);
    }

    private async Task<string> ExecutePersonaAsync(
        string skillName,
        string systemPrompt,
        string userPrompt,
        string schema,
        CancellationToken ct)
    {
        var pipeline = _pipelineProvider.GetPipeline("azure-openai");
        var result = await pipeline.ExecuteAsync(
            async token => await _aiClient.CompleteAsync(skillName, systemPrompt, userPrompt, schema, token),
            ct);

        using var _ = JsonDocument.Parse(result.Content);
        return result.Content;
    }

    private static decimal? ReadDecimal(string json, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return property.TryGetDecimal(out var value) ? value : null;
    }
}
