namespace SixToFix.Application.Exceptions;

/// <summary>Maps to HTTP 504 — Polly timeout exceeded for skill execution.</summary>
public sealed class SkillExecutionTimeoutException : Exception
{
    public string SkillName { get; }

    public SkillExecutionTimeoutException(string skillName)
        : base($"Skill '{skillName}' execution timed out.")
    {
        SkillName = skillName;
    }
}
