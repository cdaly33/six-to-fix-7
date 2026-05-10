namespace SixToFix.Application.Models;

public record SkillDefinition(
    string Name,
    string SystemPrompt,
    string OutputSchemaJson,
    int SkillIndex);
