using System.Text.Json;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Timeout;
using SixToFix.Application.Exceptions;
using SixToFix.Application.Models;
using SixToFix.Application.Services;
using SixToFix.Infrastructure.Data;

namespace SixToFix.Infrastructure.Services;

public sealed class SkillRunner : ISkillRunner
{
    private static readonly string[] SkillChain =
    [
        "6tofix-scorecard-rubric",
        "systems-maturity-scoring",
        "gap-analysis-template",
        "value-driver-rating",
        "derive-tier"
    ];

    private static readonly IReadOnlyDictionary<string, SkillDefinition> SkillDefinitions =
        new Dictionary<string, SkillDefinition>(StringComparer.Ordinal)
        {
            ["6tofix-scorecard-rubric"] = new(
                "6tofix-scorecard-rubric",
                "You are a marketing maturity scoring expert. Evaluate the client's marketing activities across 6 categories using the Six-to-Fix scoring rubric. Return a JSON object with category scores.",
                """{"type":"object","properties":{"brand":{"type":"integer"},"customer":{"type":"integer"},"offering":{"type":"integer"},"communications":{"type":"integer"},"sales":{"type":"integer"},"management":{"type":"integer"},"composite_score":{"type":"integer"},"confidence":{"type":"number"}},"required":["brand","customer","offering","communications","sales","management","composite_score","confidence"],"additionalProperties":false}""",
                0),
            ["systems-maturity-scoring"] = new(
                "systems-maturity-scoring",
                "You are a systems maturity assessment expert. Evaluate the client's marketing systems and technology stack maturity. Return a JSON object with system maturity ratings.",
                """{"type":"object","properties":{"crm_maturity":{"type":"integer"},"automation_maturity":{"type":"integer"},"analytics_maturity":{"type":"integer"},"overall_systems_score":{"type":"integer"},"recommendations":{"type":"array","items":{"type":"string"}}},"required":["crm_maturity","automation_maturity","analytics_maturity","overall_systems_score","recommendations"],"additionalProperties":false}""",
                1),
            ["gap-analysis-template"] = new(
                "gap-analysis-template",
                "You are a marketing gap analysis expert. Identify gaps between current state and target maturity across all six marketing categories. Return a structured gap analysis.",
                """{"type":"object","properties":{"gaps":{"type":"array","items":{"type":"object","properties":{"category":{"type":"string"},"current_score":{"type":"integer"},"target_score":{"type":"integer"},"gap_description":{"type":"string"},"priority":{"type":"string"}},"required":["category","current_score","target_score","gap_description","priority"],"additionalProperties":false}},"top_priority_gap":{"type":"string"}},"required":["gaps","top_priority_gap"],"additionalProperties":false}""",
                2),
            ["value-driver-rating"] = new(
                "value-driver-rating",
                "You are a marketing value driver analysis expert. Rate the key value drivers for improving this client's marketing maturity. Return driver ratings with impact scores.",
                """{"type":"object","properties":{"value_drivers":{"type":"array","items":{"type":"object","properties":{"driver":{"type":"string"},"impact_score":{"type":"integer"},"effort_score":{"type":"integer"},"rationale":{"type":"string"}},"required":["driver","impact_score","effort_score","rationale"],"additionalProperties":false}},"primary_driver":{"type":"string"}},"required":["value_drivers","primary_driver"],"additionalProperties":false}""",
                3),
            ["derive-tier"] = new(
                "derive-tier",
                "You are a marketing maturity tier classification expert. Based on all prior skill outputs, determine the client's overall marketing maturity tier (Tier 1-4) with supporting rationale.",
                """{"type":"object","properties":{"tier":{"type":"integer","minimum":1,"maximum":4},"tier_label":{"type":"string"},"composite_score":{"type":"integer"},"tier_rationale":{"type":"string"},"next_tier_requirements":{"type":"array","items":{"type":"string"}}},"required":["tier","tier_label","composite_score","tier_rationale","next_tier_requirements"],"additionalProperties":false}""",
                4)
        };

    private readonly IAIClient _aiClient;
    private readonly SixToFixDbContext _dbContext;
    private readonly IRealtimeNotifier _notifier;
    private readonly ResiliencePipelineProvider<string> _pipelineProvider;
    private readonly ILogger<SkillRunner> _logger;

    public SkillRunner(
        IAIClient aiClient,
        SixToFixDbContext dbContext,
        IRealtimeNotifier notifier,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<SkillRunner> logger)
    {
        _aiClient = aiClient;
        _dbContext = dbContext;
        _notifier = notifier;
        _pipelineProvider = pipelineProvider;
        _logger = logger;
    }

    public Task<SkillDefinition> GetSkillDefinitionAsync(string skillName, CancellationToken ct = default)
    {
        if (SkillDefinitions.TryGetValue(skillName, out var definition))
        {
            return Task.FromResult(definition);
        }

        throw new SkillNotFoundException(skillName);
    }

    public async Task<SkillResult> ExecuteSkillAsync(Guid auditRunId, string skillName, CancellationToken ct = default)
    {
        using var payload = JsonDocument.Parse("{}");
        var result = await ExecuteSkillAsync(auditRunId, skillName, payload, ct);
        return new SkillResult(skillName, result.OutputJson.RootElement.GetRawText(), result.TokensUsed, result.LatencyMs);
    }

    public async Task<SkillRunResult> ExecuteSkillAsync(
        Guid auditRunId,
        string skillName,
        JsonDocument inputPayload,
        CancellationToken ct = default)
    {
        var definition = await GetSkillDefinitionAsync(skillName, ct);
        var auditRun = await _dbContext.AuditRuns
            .FirstOrDefaultAsync(run => run.Id == auditRunId, ct)
            ?? throw new InvalidOperationException($"Audit run '{auditRunId}' was not found.");

        var now = DateTimeOffset.UtcNow;
        var skillRun = new SkillRun
        {
            Id = Guid.NewGuid(),
            TenantId = auditRun.TenantId,
            AuditRunId = auditRunId,
            SkillName = definition.Name,
            Category = ResolveCategory(inputPayload),
            SequenceIndex = definition.SkillIndex,
            Status = "running",
            StartedAt = now,
            CreatedAt = now
        };

        _dbContext.SkillRuns.Add(skillRun);
        await _dbContext.SaveChangesAsync(ct);
        await NotifyAsync(auditRunId, "skill_started", new { auditRunId, skillName = definition.Name, skillRunId = skillRun.Id }, ct);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var pipeline = _pipelineProvider.GetPipeline("azure-openai");
            var aiResult = await pipeline.ExecuteAsync(
                async token => await _aiClient.CompleteAsync(
                    definition.Name,
                    definition.SystemPrompt,
                    inputPayload.RootElement.GetRawText(),
                    definition.OutputSchemaJson,
                    token),
                ct);

            JsonDocument outputJson;
            try
            {
                outputJson = JsonDocument.Parse(aiResult.Content);
            }
            catch (JsonException ex)
            {
                throw new SkillSchemaValidationException(definition.Name, ex.Message);
            }

            stopwatch.Stop();
            skillRun.Status = "completed";
            skillRun.CompletedAt = DateTimeOffset.UtcNow;
            skillRun.ConfidenceScore = ReadDecimal(outputJson.RootElement, "confidence");
            skillRun.ActivityScore = ReadInt(outputJson.RootElement, "composite_score") ?? ReadInt(outputJson.RootElement, "overall_systems_score");
            await _dbContext.SaveChangesAsync(ct);

            await NotifyAsync(
                auditRunId,
                "skill_completed",
                new { auditRunId, skillName = definition.Name, skillRunId = skillRun.Id, latencyMs = stopwatch.ElapsedMilliseconds },
                ct);

            return new SkillRunResult(skillRun, outputJson, true, aiResult.TokensUsed, (int)stopwatch.ElapsedMilliseconds);
        }
        catch (SkillSchemaValidationException ex)
        {
            stopwatch.Stop();
            skillRun.Status = "failed";
            skillRun.CompletedAt = DateTimeOffset.UtcNow;
            skillRun.FailureReason = ex.ValidationError;
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            await NotifyAsync(auditRunId, "skill_failed", new { auditRunId, skillName = definition.Name, skillRunId = skillRun.Id, reason = "schema_validation" }, CancellationToken.None);
            _logger.LogError(ex, "Skill schema validation failed for audit run {AuditRunId} and skill {SkillName}", auditRunId, definition.Name);
            throw;
        }
        catch (TimeoutRejectedException ex)
        {
            stopwatch.Stop();
            skillRun.Status = "failed";
            skillRun.CompletedAt = DateTimeOffset.UtcNow;
            skillRun.FailureReason = "timeout";
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            await NotifyAsync(auditRunId, "skill_failed", new { auditRunId, skillName = definition.Name, skillRunId = skillRun.Id, reason = "timeout" }, CancellationToken.None);
            _logger.LogError(ex, "Skill timed out for audit run {AuditRunId} and skill {SkillName}", auditRunId, definition.Name);
            throw new SkillExecutionTimeoutException(definition.Name);
        }
        catch (BrokenCircuitException ex)
        {
            stopwatch.Stop();
            skillRun.Status = "failed";
            skillRun.CompletedAt = DateTimeOffset.UtcNow;
            skillRun.FailureReason = "circuit_open";
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            await NotifyAsync(auditRunId, "skill_failed", new { auditRunId, skillName = definition.Name, skillRunId = skillRun.Id, reason = "circuit_open" }, CancellationToken.None);
            _logger.LogError(ex, "Skill rejected by circuit breaker for audit run {AuditRunId} and skill {SkillName}", auditRunId, definition.Name);
            throw new SkillCircuitOpenException(definition.Name);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            skillRun.Status = "failed";
            skillRun.CompletedAt = DateTimeOffset.UtcNow;
            skillRun.FailureReason = ex.Message;
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            await NotifyAsync(auditRunId, "skill_failed", new { auditRunId, skillName = definition.Name, skillRunId = skillRun.Id, reason = "execution_failed" }, CancellationToken.None);
            _logger.LogError(ex, "Skill execution failed for audit run {AuditRunId} and skill {SkillName}", auditRunId, definition.Name);
            throw;
        }
    }

    public async Task MarkDownstreamSkillsStaleAsync(Guid auditRunId, string skillName, CancellationToken ct = default)
    {
        var definition = await GetSkillDefinitionAsync(skillName, ct);
        await MarkDownstreamSkillsStaleAsync(auditRunId, definition.SkillIndex, ct);
    }

    public async Task MarkDownstreamSkillsStaleAsync(Guid auditRunId, int fromSkillIndex, CancellationToken ct = default)
    {
        var staleRuns = await _dbContext.SkillRuns
            .Where(run => run.AuditRunId == auditRunId && run.SequenceIndex > fromSkillIndex)
            .ToListAsync(ct);

        foreach (var run in staleRuns)
        {
            run.Status = "stale";
            run.CompletedAt = DateTimeOffset.UtcNow;
        }

        if (staleRuns.Count > 0)
        {
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    private async Task NotifyAsync(Guid auditRunId, string method, object payload, CancellationToken ct)
    {
        try
        {
            await _notifier.SendToGroupAsync(auditRunId.ToString(), method, payload, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Realtime notification {Method} failed for audit run {AuditRunId}", method, auditRunId);
        }
    }

    private static string ResolveCategory(JsonDocument inputPayload)
    {
        if (inputPayload.RootElement.ValueKind != JsonValueKind.Object)
        {
            return "audit";
        }

        if (TryReadString(inputPayload.RootElement, "category", out var category) && category.Length <= 30)
        {
            return category;
        }

        if (TryReadString(inputPayload.RootElement, "category_id", out var categoryId) && categoryId.Length <= 30)
        {
            return categoryId;
        }

        return "audit";
    }

    private static bool TryReadString(JsonElement element, string propertyName, out string value)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        value = string.Empty;
        return false;
    }

    private static decimal? ReadDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return property.TryGetDecimal(out var value) ? value : null;
    }

    private static int? ReadInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return property.TryGetInt32(out var value) ? value : null;
    }
}
