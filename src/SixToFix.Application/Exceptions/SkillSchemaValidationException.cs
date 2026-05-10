namespace SixToFix.Application.Exceptions;

/// <summary>Maps to HTTP 502 — AI returned output that did not conform to the declared schema.</summary>
public sealed class SkillSchemaValidationException : Exception
{
    public string SkillName { get; }
    public string ValidationError { get; }

    public SkillSchemaValidationException(string skillName, string validationError)
        : base($"Skill '{skillName}' output failed schema validation: {validationError}")
    {
        SkillName = skillName;
        ValidationError = validationError;
    }
}
