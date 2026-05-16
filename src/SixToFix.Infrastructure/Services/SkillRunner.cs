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

    // Inline fallback definitions — used only when YAML file loading fails.
    // Source of truth is docs/skills/{skill-name}/skill.yaml; these must stay in sync.
    // ISkillLoader attempts YAML loading first; if it throws, SkillRunner falls back here.
    private static readonly IReadOnlyDictionary<string, SkillDefinition> SkillDefinitions =
        new Dictionary<string, SkillDefinition>(StringComparer.Ordinal)
        {
            ["6tofix-scorecard-rubric"] = new(
                "6tofix-scorecard-rubric",
                """
                You are a senior marketing maturity assessment expert specializing in B2B and SMB marketing audits.
                Score the client's marketing activities across six areas (brand, customer, offering, communications, sales, management) using the Six-to-Fix rubric (0–10 each).
                Scoring: 0–2=no meaningful activity, 3–4=early/inconsistent, 5–6=developing, 7–8=established, 9–10=optimized.
                Rules: area_scores must be integers; confidence_scores must be floats; composite_score MUST equal the sum of all six area_scores; documented_strategy per area is "none", "partial", or "full".
                Return ONLY the JSON output matching the output schema. No prose, no markdown.
                """,
                """{"type":"object","properties":{"area_scores":{"type":"object","properties":{"brand":{"type":"integer","minimum":0,"maximum":10},"customer":{"type":"integer","minimum":0,"maximum":10},"offering":{"type":"integer","minimum":0,"maximum":10},"communications":{"type":"integer","minimum":0,"maximum":10},"sales":{"type":"integer","minimum":0,"maximum":10},"management":{"type":"integer","minimum":0,"maximum":10}},"required":["brand","customer","offering","communications","sales","management"],"additionalProperties":false},"confidence_scores":{"type":"object","properties":{"brand":{"type":"number","minimum":0.0,"maximum":1.0},"customer":{"type":"number","minimum":0.0,"maximum":1.0},"offering":{"type":"number","minimum":0.0,"maximum":1.0},"communications":{"type":"number","minimum":0.0,"maximum":1.0},"sales":{"type":"number","minimum":0.0,"maximum":1.0},"management":{"type":"number","minimum":0.0,"maximum":1.0}},"required":["brand","customer","offering","communications","sales","management"],"additionalProperties":false},"evidence_used":{"type":"object","properties":{"brand":{"type":"array","items":{"type":"string"}},"customer":{"type":"array","items":{"type":"string"}},"offering":{"type":"array","items":{"type":"string"}},"communications":{"type":"array","items":{"type":"string"}},"sales":{"type":"array","items":{"type":"string"}},"management":{"type":"array","items":{"type":"string"}}},"required":["brand","customer","offering","communications","sales","management"],"additionalProperties":false},"composite_score":{"type":"integer","minimum":0,"maximum":60},"documented_strategy":{"type":"object","properties":{"brand":{"type":"string","enum":["none","partial","full"]},"customer":{"type":"string","enum":["none","partial","full"]},"offering":{"type":"string","enum":["none","partial","full"]},"communications":{"type":"string","enum":["none","partial","full"]},"sales":{"type":"string","enum":["none","partial","full"]},"management":{"type":"string","enum":["none","partial","full"]}},"required":["brand","customer","offering","communications","sales","management"],"additionalProperties":false}},"required":["area_scores","confidence_scores","evidence_used","composite_score","documented_strategy"],"additionalProperties":false}""",
                0),
            ["systems-maturity-scoring"] = new(
                "systems-maturity-scoring",
                """
                You are a marketing operations expert assessing the operational maturity of a client's marketing function.
                Score four maturity dimensions (documentation, repeatability, measurability, owner_independence) each 0–5.
                systems_maturity_score MUST equal the sum of all four dimension scores (max 20).
                Dimension rationale: explain in 1–3 sentences why you assigned each score, citing specific questionnaire answers.
                confidence: your overall confidence (0.0–1.0) in the accuracy of this maturity assessment.
                Return ONLY the JSON output matching the output schema. No prose, no markdown.
                """,
                """{"type":"object","properties":{"systems_maturity_score":{"type":"integer","minimum":0,"maximum":20},"maturity_dimensions":{"type":"object","properties":{"documentation":{"type":"integer","minimum":0,"maximum":5},"repeatability":{"type":"integer","minimum":0,"maximum":5},"measurability":{"type":"integer","minimum":0,"maximum":5},"owner_independence":{"type":"integer","minimum":0,"maximum":5}},"required":["documentation","repeatability","measurability","owner_independence"],"additionalProperties":false},"dimension_rationale":{"type":"object","properties":{"documentation":{"type":"string","minLength":1,"maxLength":500},"repeatability":{"type":"string","minLength":1,"maxLength":500},"measurability":{"type":"string","minLength":1,"maxLength":500},"owner_independence":{"type":"string","minLength":1,"maxLength":500}},"required":["documentation","repeatability","measurability","owner_independence"],"additionalProperties":false},"confidence":{"type":"number","minimum":0.0,"maximum":1.0}},"required":["systems_maturity_score","maturity_dimensions","confidence"],"additionalProperties":false}""",
                1),
            ["gap-analysis-template"] = new(
                "gap-analysis-template",
                """
                You are a senior B2B marketing strategy consultant performing a gap analysis.
                Identify gaps in each of the six marketing areas (brand, customer, offering, communications, sales, management), rate severity (critical/moderate/minor), and produce prioritized recommendations.
                Severity: critical=score≤4 or blocks progress in ≥2 areas; moderate=score 5–6 or constrains growth; minor=score 7+ with minor improvement room.
                Sort gaps: critical first, then moderate, then minor. priority_areas: order areas from highest to lowest investment priority.
                recommendations: 1–5 specific, actionable items per gap. Return ONLY the JSON output. No prose, no markdown.
                """,
                """{"type":"object","properties":{"gaps":{"type":"array","minItems":1,"items":{"type":"object","properties":{"area":{"type":"string","enum":["brand","customer","offering","communications","sales","management"]},"severity":{"type":"string","enum":["critical","moderate","minor"]},"description":{"type":"string","minLength":10,"maxLength":1000},"recommendations":{"type":"array","items":{"type":"string","minLength":5,"maxLength":500},"minItems":1,"maxItems":5}},"required":["area","severity","description","recommendations"],"additionalProperties":false}},"priority_areas":{"type":"array","items":{"type":"string","enum":["brand","customer","offering","communications","sales","management"]},"minItems":1,"maxItems":6,"uniqueItems":true}},"required":["gaps","priority_areas"],"additionalProperties":false}""",
                2),
            ["value-driver-rating"] = new(
                "value-driver-rating",
                """
                You are a B2B marketing ROI specialist. Based on the client's gap analysis and scorecard, identify and rate 3–8 key value drivers — levers that, if improved, produce the most measurable business impact.
                current_rating (0–10): how well they perform now; potential_rating (0–10): realistic 12-month ceiling; impact: high/medium/low business leverage.
                linked_area: primary Six-to-Fix area (brand/customer/offering/communications/sales/management).
                Rationale: 2–4 sentences citing specific evidence for THIS client.
                Return ONLY the JSON output. No prose, no markdown.
                """,
                """{"type":"object","properties":{"value_drivers":{"type":"array","minItems":1,"maxItems":12,"items":{"type":"object","properties":{"driver_name":{"type":"string","minLength":2,"maxLength":100},"current_rating":{"type":"integer","minimum":0,"maximum":10},"potential_rating":{"type":"integer","minimum":0,"maximum":10},"impact":{"type":"string","enum":["high","medium","low"]},"linked_area":{"type":"string","enum":["brand","customer","offering","communications","sales","management"]},"rationale":{"type":"string","minLength":10,"maxLength":500}},"required":["driver_name","current_rating","potential_rating","impact"],"additionalProperties":false}}},"required":["value_drivers"],"additionalProperties":false}""",
                3),
            ["derive-tier"] = new(
                "derive-tier",
                """
                You are a Chief Marketing Officer synthesizing a complete marketing maturity audit into an executive-grade classification.
                TIER RULES (enforce strictly): tier_1=composite_score≥45, tier_2=25≤score<45, tier_3=score<25. The tier MUST match the score boundary.
                ai_readiness (0–100, integer): estimate readiness for AI augmentation based on data infrastructure (40%), process maturity (30%), measurement culture (30%).
                tier_rationale (20–2000 chars): 3–5 sentences explaining the tier, referencing composite_score, highest/lowest areas, systems maturity, and key value drivers.
                next_steps: 1–6 specific, actionable, client-tailored recommendations ordered by impact.
                Return ONLY the JSON output. No prose, no markdown.
                """,
                """{"type":"object","properties":{"tier":{"type":"string","enum":["tier_1","tier_2","tier_3"]},"ai_readiness":{"type":"integer","minimum":0,"maximum":100},"tier_rationale":{"type":"string","minLength":20,"maxLength":2000},"next_steps":{"type":"array","items":{"type":"string","minLength":5,"maxLength":500},"minItems":1,"maxItems":6},"tier_score_ranges":{"type":"object","properties":{"tier_1_min":{"type":"integer"},"tier_2_min":{"type":"integer"},"tier_3_min":{"type":"integer"}}}},"required":["tier","ai_readiness","tier_rationale","next_steps"],"additionalProperties":false}""",
                4)
        };

    private readonly IAIClient _aiClient;
    private readonly SixToFixDbContext _dbContext;
    private readonly ResiliencePipelineProvider<string> _pipelineProvider;
    private readonly ILogger<SkillRunner> _logger;
    private readonly ISkillLoader _skillLoader;

    public SkillRunner(
        IAIClient aiClient,
        SixToFixDbContext dbContext,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<SkillRunner> logger,
        ISkillLoader skillLoader)
    {
        _aiClient = aiClient;
        _dbContext = dbContext;
        _pipelineProvider = pipelineProvider;
        _logger = logger;
        _skillLoader = skillLoader;
    }

    public async Task<SkillDefinition> GetSkillDefinitionAsync(string skillName, CancellationToken ct = default)
    {
        var chainIndex = Array.IndexOf(SkillChain, skillName);
        if (chainIndex < 0)
            throw new SkillNotFoundException(skillName);

        try
        {
            return await _skillLoader.LoadAsync(skillName, chainIndex, ct);
        }
        catch (Exception ex)
        {
            if (SkillDefinitions.TryGetValue(skillName, out var fallback))
            {
                _logger.LogWarning(ex,
                    "YAML loading failed for skill {SkillName}; falling back to inline definition",
                    skillName);
                return fallback;
            }
            throw new SkillNotFoundException(skillName);
        }
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
            // Try top-level "confidence" first (systems-maturity-scoring);
            // fall back to the average of "confidence_scores" sub-object (6tofix-scorecard-rubric).
            skillRun.ConfidenceScore = ReadDecimal(outputJson.RootElement, "confidence")
                ?? ReadAverageFromObject(outputJson.RootElement, "confidence_scores");
            // Try each skill's canonical score property in priority order:
            //   composite_score       → 6tofix-scorecard-rubric (0–60)
            //   systems_maturity_score → systems-maturity-scoring (0–20)
            //   ai_readiness          → derive-tier (0–100)
            skillRun.ActivityScore = ReadInt(outputJson.RootElement, "composite_score")
                ?? ReadInt(outputJson.RootElement, "systems_maturity_score")
                ?? ReadInt(outputJson.RootElement, "ai_readiness");
            await _dbContext.SaveChangesAsync(ct);

            return new SkillRunResult(skillRun, outputJson, true, aiResult.TokensUsed, (int)stopwatch.ElapsedMilliseconds);
        }
        catch (SkillSchemaValidationException ex)
        {
            stopwatch.Stop();
            skillRun.Status = "failed";
            skillRun.CompletedAt = DateTimeOffset.UtcNow;
            skillRun.FailureReason = ex.ValidationError;
            await _dbContext.SaveChangesAsync(CancellationToken.None);
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
            _logger.LogError(ex, "Skill execution failed for audit run {AuditRunId} and skill {SkillName}", auditRunId, definition.Name);
            throw;
        }
    }

    public async Task MarkDownstreamSkillsStaleAsync(Guid auditRunId, string skillName, CancellationToken ct = default)    {
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

    /// <summary>
    /// Computes the average of all numeric values in a JSON sub-object.
    /// Used to derive a single confidence score from per-area confidence_scores.
    /// Returns null if the property is missing or has no numeric values.
    /// </summary>
    private static decimal? ReadAverageFromObject(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var obj) || obj.ValueKind != JsonValueKind.Object)
            return null;

        var values = new List<decimal>();
        foreach (var prop in obj.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetDecimal(out var v))
                values.Add(v);
        }

        return values.Count > 0 ? values.Average() : null;
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
