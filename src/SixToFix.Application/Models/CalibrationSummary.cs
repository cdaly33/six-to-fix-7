namespace SixToFix.Application.Models;

public record CalibrationSummary(
    int TotalOverrides,
    decimal AverageDelta,
    IReadOnlyList<string> TopReasonCodes);
