using System.Text.Json;
using SixToFix.Application.Models;

namespace SixToFix.Application.Services;

public record SkillResult(string SkillName, string? OutputJson, int TokensUsed, int LatencyMs);

public interface ISkillRunner
{
    Task<SkillResult> ExecuteSkillAsync(Guid auditRunId, string skillName, CancellationToken ct = default);
    Task<SkillRunResult> ExecuteSkillAsync(Guid auditRunId, string skillName, JsonDocument inputPayload, CancellationToken ct = default);
    Task<SkillDefinition> GetSkillDefinitionAsync(string skillName, CancellationToken ct = default);
    Task MarkDownstreamSkillsStaleAsync(Guid auditRunId, string skillName, CancellationToken ct = default);
    Task MarkDownstreamSkillsStaleAsync(Guid auditRunId, int fromSkillIndex, CancellationToken ct = default);
}
