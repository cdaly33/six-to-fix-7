namespace SixToFix.Application.Models;

public record SearchResult(
    IReadOnlyList<IDictionary<string, object>> Documents,
    long TotalCount);
