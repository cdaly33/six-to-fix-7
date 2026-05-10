namespace SixToFix.Application.Models;

public record PolicyEvaluationSummary(
    int TotalFlags,
    int WarningCount,
    int TriggerCount,
    IReadOnlyList<string> TriggerRuleNames,
    bool RequiresEscalation);
