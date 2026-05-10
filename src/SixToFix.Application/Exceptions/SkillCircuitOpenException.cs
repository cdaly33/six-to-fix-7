namespace SixToFix.Application.Exceptions;

/// <summary>Maps to HTTP 503 — circuit breaker is open for the Azure OpenAI endpoint.</summary>
public sealed class SkillCircuitOpenException : Exception
{
    public string SkillName { get; }

    public SkillCircuitOpenException(string skillName)
        : base($"Skill '{skillName}' execution rejected — circuit breaker is open.")
    {
        SkillName = skillName;
    }
}
