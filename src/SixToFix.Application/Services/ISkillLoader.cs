using SixToFix.Application.Models;

namespace SixToFix.Application.Services;

/// <summary>
/// Loads a skill definition from a YAML file in docs/skills/{skillName}/skill.yaml.
/// </summary>
public interface ISkillLoader
{
    /// <summary>
    /// Loads the SkillDefinition for the named skill.
    /// </summary>
    /// <param name="skillName">Directory name under docs/skills/ (e.g., "6tofix-scorecard-rubric").</param>
    /// <param name="skillIndex">Zero-based position of this skill in the execution chain.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SkillDefinition> LoadAsync(string skillName, int skillIndex, CancellationToken ct = default);
}
