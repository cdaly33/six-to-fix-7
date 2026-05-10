namespace SixToFix.Application.Models;

public record AiCompletionResult(
    string Content,
    int TokensUsed,
    string ModelId,
    string FinishReason);
