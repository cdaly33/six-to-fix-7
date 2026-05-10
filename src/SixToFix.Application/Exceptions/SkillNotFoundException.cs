namespace SixToFix.Application.Exceptions;

public sealed class SkillNotFoundException : Exception
{
    public string SkillName { get; }

    public SkillNotFoundException(string skillName)
        : base($"Skill '{skillName}' not found in the skill chain.")
    {
        SkillName = skillName;
    }
}
