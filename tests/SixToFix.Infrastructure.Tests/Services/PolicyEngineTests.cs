using FluentAssertions;
using SixToFix.Application.Models;
using SixToFix.Infrastructure.Services;
using Xunit;

namespace SixToFix.Infrastructure.Tests.Services;

/// <summary>
/// Pure unit tests for PolicyEngine's synchronous evaluation methods.
/// No database access — the sync methods (EvaluateCategory, RequiresCouncilEscalation,
/// SummarizeFlags) do not touch DbContext; null! is safe here.
/// </summary>
public sealed class PolicyEngineTests
{
    private static readonly PolicyEngine Sut = new(null!);
    private static readonly Guid AuditRunId = Guid.NewGuid();

    // ──────────────────────────────────────────────────────────────
    // LOW_CONFIDENCE  (Trigger when confidence < 0.6)
    // ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.59)]
    [InlineData(0.599)]
    public void LowConfidence_BelowThreshold_AddsTriggerFlag(double confidence)
    {
        var payload = BuildPayload(confidence: (decimal)confidence);
        var flags = Sut.EvaluateCategory(payload, NeutralContext());

        flags.Should().ContainSingle(f => f.Rule == "LOW_CONFIDENCE" && f.Severity == "Trigger");
    }

    [Theory]
    [InlineData(0.6)]
    [InlineData(0.601)]
    [InlineData(1.0)]
    public void LowConfidence_AtOrAboveThreshold_NoFlag(double confidence)
    {
        var payload = BuildPayload(confidence: (decimal)confidence, evidence: ["ev1", "ev2"]);
        var flags = Sut.EvaluateCategory(payload, NeutralContext());

        flags.Should().NotContain(f => f.Rule == "LOW_CONFIDENCE");
    }

    // ──────────────────────────────────────────────────────────────
    // MISSING_EVIDENCE  (Warning when evidence list is empty)
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void MissingEvidence_EmptyList_AddsWarningFlag()
    {
        var payload = BuildPayload(evidence: []);
        var flags = Sut.EvaluateCategory(payload, NeutralContext());

        flags.Should().ContainSingle(f => f.Rule == "MISSING_EVIDENCE" && f.Severity == "Warning");
    }

    [Fact]
    public void MissingEvidence_OneItem_NoMissingEvidenceFlag()
    {
        var payload = BuildPayload(confidence: 0.9m, evidence: ["ev1"]);
        var flags = Sut.EvaluateCategory(payload, NeutralContext());

        flags.Should().NotContain(f => f.Rule == "MISSING_EVIDENCE");
    }

    [Fact]
    public void MissingEvidence_TwoItems_NoFlag()
    {
        var payload = BuildPayload(confidence: 0.9m, evidence: ["ev1", "ev2"]);
        var flags = Sut.EvaluateCategory(payload, NeutralContext());

        flags.Should().NotContain(f => f.Rule == "MISSING_EVIDENCE");
    }

    // ──────────────────────────────────────────────────────────────
    // BENCHMARK_OUTLIER  (Trigger when |score - median| > 2 × stdDev)
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void BenchmarkOutlier_ExactlyAt2Sigma_NoFlag()
    {
        // deviation = 2, stdDev = 1  =>  deviation == threshold  →  no flag
        var payload = BuildPayload(activityScore: 7m, confidence: 0.9m, evidence: ["ev1", "ev2"]);
        var context = new PolicyEvaluationContext(TenantMedianScore: 5m, TenantStdDev: 1m, AuditRunId);

        var flags = Sut.EvaluateCategory(payload, context);

        flags.Should().NotContain(f => f.Rule == "BENCHMARK_OUTLIER");
    }

    [Fact]
    public void BenchmarkOutlier_Above2Sigma_AddsTriggerFlag()
    {
        // deviation = 3, stdDev = 1  =>  3 > 2 → Trigger
        var payload = BuildPayload(activityScore: 8m, confidence: 0.9m, evidence: ["ev1", "ev2"]);
        var context = new PolicyEvaluationContext(TenantMedianScore: 5m, TenantStdDev: 1m, AuditRunId);

        var flags = Sut.EvaluateCategory(payload, context);

        flags.Should().ContainSingle(f => f.Rule == "BENCHMARK_OUTLIER" && f.Severity == "Trigger");
    }

    [Fact]
    public void BenchmarkOutlier_ZeroStdDev_NoFlag()
    {
        // stdDev = 0 → threshold = 0. deviation = 2 > 0 → flag IS raised
        // Actually: deviation = |7 - 5| = 2, threshold = 2*0 = 0 → 2 > 0 → flag raised
        var payload = BuildPayload(activityScore: 7m, confidence: 0.9m, evidence: ["ev1", "ev2"]);
        var context = new PolicyEvaluationContext(TenantMedianScore: 5m, TenantStdDev: 0m, AuditRunId);

        var flags = Sut.EvaluateCategory(payload, context);

        flags.Should().ContainSingle(f => f.Rule == "BENCHMARK_OUTLIER");
    }

    [Fact]
    public void BenchmarkOutlier_ScoreEqualsMedian_NoFlag()
    {
        var payload = BuildPayload(activityScore: 5m, confidence: 0.9m, evidence: ["ev1", "ev2"]);
        var context = new PolicyEvaluationContext(TenantMedianScore: 5m, TenantStdDev: 1m, AuditRunId);

        var flags = Sut.EvaluateCategory(payload, context);

        flags.Should().NotContain(f => f.Rule == "BENCHMARK_OUTLIER");
    }

    [Fact]
    public void BenchmarkOutlier_Detail_ContainsSigmaInfo()
    {
        var payload = BuildPayload(activityScore: 8m, confidence: 0.9m, evidence: ["ev1", "ev2"]);
        var context = new PolicyEvaluationContext(TenantMedianScore: 5m, TenantStdDev: 1m, AuditRunId);

        var flags = Sut.EvaluateCategory(payload, context);

        var flag = flags.Single(f => f.Rule == "BENCHMARK_OUTLIER");
        flag.Detail.Should().Contain("3").And.Contain("σ");
    }

    // ──────────────────────────────────────────────────────────────
    // INSUFFICIENT_EVIDENCE  (Warning when evidence.Count == 1)
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void InsufficientEvidence_ExactlyOneItem_AddsWarningFlag()
    {
        var payload = BuildPayload(confidence: 0.9m, evidence: ["only-one"]);
        var flags = Sut.EvaluateCategory(payload, NeutralContext());

        flags.Should().ContainSingle(f => f.Rule == "INSUFFICIENT_EVIDENCE" && f.Severity == "Warning");
    }

    [Fact]
    public void InsufficientEvidence_TwoItems_NoFlag()
    {
        var payload = BuildPayload(confidence: 0.9m, evidence: ["ev1", "ev2"]);
        var flags = Sut.EvaluateCategory(payload, NeutralContext());

        flags.Should().NotContain(f => f.Rule == "INSUFFICIENT_EVIDENCE");
    }

    [Fact]
    public void InsufficientEvidence_ZeroItems_NoFlag()
    {
        // Zero items → MISSING_EVIDENCE fires, not INSUFFICIENT_EVIDENCE
        var payload = BuildPayload(evidence: []);
        var flags = Sut.EvaluateCategory(payload, NeutralContext());

        flags.Should().NotContain(f => f.Rule == "INSUFFICIENT_EVIDENCE");
    }

    // ──────────────────────────────────────────────────────────────
    // SCORE_STRATEGY_MISMATCH  (Trigger when score > 7 and no strategy)
    // ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(8, null)]
    [InlineData(8, "none")]
    [InlineData(8, "NONE")]
    [InlineData(10, null)]
    public void ScoreStrategyMismatch_HighScoreNoStrategy_AddsTriggerFlag(
        int score, string? strategy)
    {
        var payload = BuildPayload(activityScore: score, confidence: 0.9m,
            evidence: ["ev1", "ev2"], strategy: strategy);
        var flags = Sut.EvaluateCategory(payload, NeutralContext());

        flags.Should().ContainSingle(f => f.Rule == "SCORE_STRATEGY_MISMATCH" && f.Severity == "Trigger");
    }

    [Theory]
    [InlineData(7, null)]       // boundary: score == 7, not > 7
    [InlineData(6, null)]
    [InlineData(8, "differentiation")]  // high score but has strategy
    public void ScoreStrategyMismatch_BoundaryOrHasStrategy_NoFlag(
        int score, string? strategy)
    {
        var payload = BuildPayload(activityScore: score, confidence: 0.9m,
            evidence: ["ev1", "ev2"], strategy: strategy);
        var flags = Sut.EvaluateCategory(payload, NeutralContext());

        flags.Should().NotContain(f => f.Rule == "SCORE_STRATEGY_MISMATCH");
    }

    // ──────────────────────────────────────────────────────────────
    // RequiresCouncilEscalation
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void RequiresCouncilEscalation_NoFlags_ReturnsFalse()
    {
        Sut.RequiresCouncilEscalation([]).Should().BeFalse();
    }

    [Fact]
    public void RequiresCouncilEscalation_OnlyWarnings_ReturnsFalse()
    {
        var flags = new[]
        {
            new PolicyFlagModel("MISSING_EVIDENCE", "Warning", null),
            new PolicyFlagModel("INSUFFICIENT_EVIDENCE", "Warning", null)
        };
        Sut.RequiresCouncilEscalation(flags).Should().BeFalse();
    }

    [Fact]
    public void RequiresCouncilEscalation_OneTrigger_ReturnsTrue()
    {
        var flags = new[] { new PolicyFlagModel("LOW_CONFIDENCE", "Trigger", null) };
        Sut.RequiresCouncilEscalation(flags).Should().BeTrue();
    }

    [Fact]
    public void RequiresCouncilEscalation_MixedSeverities_ReturnsTrue()
    {
        var flags = new[]
        {
            new PolicyFlagModel("MISSING_EVIDENCE", "Warning", null),
            new PolicyFlagModel("LOW_CONFIDENCE", "Trigger", null)
        };
        Sut.RequiresCouncilEscalation(flags).Should().BeTrue();
    }

    [Fact]
    public void RequiresCouncilEscalation_SeverityIsCaseSensitive_WarningSeverityDoesNotEscalate()
    {
        // "trigger" ≠ "Trigger" — should NOT escalate
        var flags = new[] { new PolicyFlagModel("FAKE_RULE", "trigger", null) };
        Sut.RequiresCouncilEscalation(flags).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────
    // SummarizeFlags — combinations
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void SummarizeFlags_NoFlags_AllZerosNoEscalation()
    {
        var summary = Sut.SummarizeFlags([]);

        summary.TotalFlags.Should().Be(0);
        summary.WarningCount.Should().Be(0);
        summary.TriggerCount.Should().Be(0);
        summary.TriggerRuleNames.Should().BeEmpty();
        summary.RequiresEscalation.Should().BeFalse();
    }

    [Fact]
    public void SummarizeFlags_AllWarnings_NoEscalation()
    {
        var flags = new[]
        {
            new PolicyFlagModel("MISSING_EVIDENCE", "Warning", null),
            new PolicyFlagModel("INSUFFICIENT_EVIDENCE", "Warning", null)
        };
        var summary = Sut.SummarizeFlags(flags);

        summary.TotalFlags.Should().Be(2);
        summary.WarningCount.Should().Be(2);
        summary.TriggerCount.Should().Be(0);
        summary.RequiresEscalation.Should().BeFalse();
    }

    [Fact]
    public void SummarizeFlags_MixedFlags_CorrectCountsAndEscalation()
    {
        var flags = new[]
        {
            new PolicyFlagModel("MISSING_EVIDENCE", "Warning", null),
            new PolicyFlagModel("LOW_CONFIDENCE", "Trigger", null),
            new PolicyFlagModel("SCORE_STRATEGY_MISMATCH", "Trigger", null)
        };
        var summary = Sut.SummarizeFlags(flags);

        summary.TotalFlags.Should().Be(3);
        summary.WarningCount.Should().Be(1);
        summary.TriggerCount.Should().Be(2);
        summary.TriggerRuleNames.Should().BeEquivalentTo(["LOW_CONFIDENCE", "SCORE_STRATEGY_MISMATCH"]);
        summary.RequiresEscalation.Should().BeTrue();
    }

    [Fact]
    public void SummarizeFlags_AllTriggers_AllFlagsEscalate()
    {
        var flags = new[]
        {
            new PolicyFlagModel("LOW_CONFIDENCE", "Trigger", null),
            new PolicyFlagModel("BENCHMARK_OUTLIER", "Trigger", "detail"),
            new PolicyFlagModel("SCORE_STRATEGY_MISMATCH", "Trigger", null)
        };
        var summary = Sut.SummarizeFlags(flags);

        summary.TotalFlags.Should().Be(3);
        summary.WarningCount.Should().Be(0);
        summary.TriggerCount.Should().Be(3);
        summary.RequiresEscalation.Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────────────
    // Full combination: realistic "all-clear" payload
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void EvaluateCategory_GoodPayload_NoFlags()
    {
        var payload = BuildPayload(
            activityScore: 7m,
            confidence: 0.9m,
            evidence: ["ev1", "ev2"],
            strategy: "differentiation");
        var context = new PolicyEvaluationContext(
            TenantMedianScore: 6m, TenantStdDev: 2m, AuditRunId);

        var flags = Sut.EvaluateCategory(payload, context);

        flags.Should().BeEmpty();
    }

    [Fact]
    public void EvaluateCategory_WorstCasePayload_AllFiveFlagsNotPossible_FourPossible()
    {
        // LOW_CONFIDENCE + MISSING_EVIDENCE triggers, INSUFFICIENT not (0 evidence),
        // BENCHMARK_OUTLIER with zero stdDev, SCORE_STRATEGY_MISMATCH score > 7.
        var payload = BuildPayload(
            activityScore: 9m,
            confidence: 0.0m,
            evidence: [],
            strategy: null);
        var context = new PolicyEvaluationContext(
            TenantMedianScore: 5m, TenantStdDev: 0m, AuditRunId);

        var flags = Sut.EvaluateCategory(payload, context);

        flags.Select(f => f.Rule).Should().BeEquivalentTo(
            ["LOW_CONFIDENCE", "MISSING_EVIDENCE", "BENCHMARK_OUTLIER", "SCORE_STRATEGY_MISMATCH"]);
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    private static CategoryResultPayload BuildPayload(
        decimal activityScore = 5m,
        decimal confidence = 0.9m,
        IReadOnlyList<string>? evidence = null,
        string? strategy = "differentiation",
        int? activityScoreInt = null)
    {
        if (activityScoreInt.HasValue) activityScore = activityScoreInt.Value;
        return new CategoryResultPayload(
            "brand",
            activityScore,
            confidence,
            evidence ?? ["ev1", "ev2"],
            strategy);
    }

    private static PolicyEvaluationContext NeutralContext() =>
        new(TenantMedianScore: 5m, TenantStdDev: 2m, AuditRunId);
}
