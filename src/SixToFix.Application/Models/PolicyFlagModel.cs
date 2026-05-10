namespace SixToFix.Application.Models;

public record PolicyFlagModel(
    string Rule,
    string Severity,
    string? Detail);
