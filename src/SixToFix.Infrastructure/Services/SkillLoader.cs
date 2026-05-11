using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using SixToFix.Application.Models;
using SixToFix.Application.Services;
using YamlDotNet.Serialization;

namespace SixToFix.Infrastructure.Services;

/// <summary>
/// Loads SkillDefinitions from docs/skills/{skillName}/skill.yaml using YamlDotNet.
/// Registered as Singleton — stateless and safe for concurrent reads. SkillRunner (Scoped)
/// injecting a Singleton follows the safe one-way dependency direction per ADR-001.
/// </summary>
public sealed class SkillLoader : ISkillLoader
{
    private static readonly string[] RequiredFields = ["name", "system_prompt", "output_schema"];

    private readonly ILogger<SkillLoader> _logger;
    private readonly string _skillsRoot;
    private readonly IDeserializer _deserializer;

    public SkillLoader(ILogger<SkillLoader> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _skillsRoot = ResolveSkillsRoot(environment.ContentRootPath);
        _deserializer = new DeserializerBuilder().Build();
    }

    public Task<SkillDefinition> LoadAsync(string skillName, int skillIndex, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var skillFile = Path.Combine(_skillsRoot, skillName, "skill.yaml");

        if (!File.Exists(skillFile))
            throw new InvalidOperationException(
                $"Skill YAML file not found at '{skillFile}'. " +
                $"Ensure docs/skills/{skillName}/skill.yaml exists relative to the repository root.");

        Dictionary<object, object> raw;
        using (var reader = File.OpenText(skillFile))
        {
            raw = _deserializer.Deserialize<Dictionary<object, object>>(reader);
        }

        ValidateRequiredFields(skillName, raw);

        var name = raw["name"]!.ToString()!;
        var systemPrompt = raw["system_prompt"]!.ToString()!;
        var outputSchemaJson = ConvertToJson(raw["output_schema"]);

        _logger.LogDebug("Loaded skill definition {SkillName} from {SkillFile}", skillName, skillFile);

        return Task.FromResult(new SkillDefinition(name, systemPrompt, outputSchemaJson, skillIndex));
    }

    private static void ValidateRequiredFields(string skillName, Dictionary<object, object> raw)
    {
        foreach (var field in RequiredFields)
        {
            if (!raw.ContainsKey(field) || raw[field] is null)
                throw new InvalidOperationException(
                    $"Skill '{skillName}' YAML is missing required field '{field}'.");
        }

        if (string.IsNullOrWhiteSpace(raw["name"]?.ToString()))
            throw new InvalidOperationException($"Skill '{skillName}' YAML has an empty 'name' field.");

        if (string.IsNullOrWhiteSpace(raw["system_prompt"]?.ToString()))
            throw new InvalidOperationException($"Skill '{skillName}' YAML has an empty 'system_prompt' field.");
    }

    private static string ConvertToJson(object? yamlObject) =>
        JsonSerializer.Serialize(NormalizeYamlValue(yamlObject));

    private static object? NormalizeYamlValue(object? value) =>
        value switch
        {
            Dictionary<object, object> dict => dict.ToDictionary(
                kvp => kvp.Key?.ToString() ?? string.Empty,
                kvp => NormalizeYamlValue(kvp.Value)),
            List<object> list => list.Select(NormalizeYamlValue).ToList(),
            _ => value
        };

    /// <summary>
    /// Walks up the directory tree from <paramref name="startPath"/> looking for a
    /// "docs/skills" subdirectory. Falls back to walking up from AppContext.BaseDirectory.
    /// This resolves correctly in dev (content root = project dir), local test runs, and
    /// published deployments where docs/ is co-located with the app.
    /// </summary>
    private static string ResolveSkillsRoot(string startPath)
    {
        var candidate = WalkUp(startPath);
        if (candidate != null)
            return candidate;

        candidate = WalkUp(AppContext.BaseDirectory);
        if (candidate != null)
            return candidate;

        throw new InvalidOperationException(
            "Could not locate 'docs/skills' directory starting from either the content root " +
            $"('{startPath}') or the application base directory ('{AppContext.BaseDirectory}'). " +
            "Ensure skill YAML files are present under docs/skills/ relative to the repository root.");
    }

    private static string? WalkUp(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "docs", "skills");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
