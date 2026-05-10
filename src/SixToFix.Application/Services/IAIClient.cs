using SixToFix.Application.Models;

namespace SixToFix.Application.Services;

public interface IAIClient
{
    Task<AiCompletionResult> CompleteAsync(string skillName, string systemPrompt, string userPrompt, string outputSchemaJson, CancellationToken ct = default);
}
