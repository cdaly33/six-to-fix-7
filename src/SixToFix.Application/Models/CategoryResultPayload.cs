namespace SixToFix.Application.Models;

public record CategoryResultPayload(
    string CategoryId,
    decimal ActivityScore,
    decimal Confidence,
    IReadOnlyList<string> Evidence,
    string? DocumentedStrategy);
