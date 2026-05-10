using System.Text.Json;

namespace SixToFix.Application.Models;

public record SkillRunResult(
    SkillRun SkillRun,
    JsonDocument OutputJson,
    bool IsSchemaValid,
    int TokensUsed,
    int LatencyMs);
