using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SixToFix.Infrastructure.Services;
using YamlDotNet.Serialization;

namespace SixToFix.Infrastructure.Tests.Services;

public sealed class SkillYamlValidationTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    private static readonly string SkillsRoot = Path.Combine(RepositoryRoot, "docs", "skills");
    private static readonly IDeserializer Deserializer = new DeserializerBuilder().Build();
    private static readonly string[] RequiredFields = ["name", "version", "system_prompt", "input_schema", "output_schema"];
    private static readonly string[] ExpectedSkillNames =
    [
        "6tofix-scorecard-rubric",
        "systems-maturity-scoring",
        "gap-analysis-template",
        "value-driver-rating",
        "derive-tier"
    ];

    public static IEnumerable<object[]> SkillFiles() =>
        Directory.GetFiles(SkillsRoot, "skill.yaml", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new object[] { path });

    public static IEnumerable<object[]> SkillNamesWithIndex() =>
        ExpectedSkillNames.Select((name, i) => new object[] { name, i });

    [Fact]
    public void SkillYamlFiles_ExistForEveryExpectedSkill()
    {
        var actual = Directory.GetDirectories(SkillsRoot)
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        actual.Should().BeEquivalentTo(ExpectedSkillNames);
    }

    [Theory]
    [MemberData(nameof(SkillFiles))]
    public void SkillYaml_IsValidYaml_AndContainsRequiredFields(string skillFile)
    {
        var root = DeserializeSkill(skillFile);

        foreach (var field in RequiredFields)
        {
            root.Should().ContainKey(field);
            root[field].Should().NotBeNull();
        }
    }

    [Theory]
    [MemberData(nameof(SkillFiles))]
    public void SkillYaml_NameMatchesFolder_AndSchemasAreObjectMaps(string skillFile)
    {
        var root = DeserializeSkill(skillFile);
        var expectedName = Path.GetFileName(Path.GetDirectoryName(skillFile)!);

        root["name"].Should().Be(expectedName);
        root["system_prompt"].Should().BeOfType<string>().Which.Should().NotBeNullOrWhiteSpace();
        root["input_schema"].Should().BeAssignableTo<IDictionary<object, object>>();
        root["output_schema"].Should().BeAssignableTo<IDictionary<object, object>>();
    }

    [Theory]
    [MemberData(nameof(SkillNamesWithIndex))]
    public async Task SkillLoader_LoadAsync_ReturnsValidSkillDefinition(string skillName, int skillIndex)
    {
        var env = Substitute.For<IHostEnvironment>();
        env.ContentRootPath.Returns(RepositoryRoot);

        var loader = new SkillLoader(NullLogger<SkillLoader>.Instance, env);
        var definition = await loader.LoadAsync(skillName, skillIndex);

        definition.Name.Should().Be(skillName);
        definition.SkillIndex.Should().Be(skillIndex);
        definition.SystemPrompt.Should().NotBeNullOrWhiteSpace();
        definition.OutputSchemaJson.Should().NotBeNullOrWhiteSpace();

        // Output schema must be valid JSON
        var act = () => System.Text.Json.JsonDocument.Parse(definition.OutputSchemaJson);
        act.Should().NotThrow();
    }

    private static Dictionary<object, object> DeserializeSkill(string skillFile)
    {
        File.Exists(skillFile).Should().BeTrue();
        using var reader = File.OpenText(skillFile);
        return Deserializer.Deserialize<Dictionary<object, object>>(reader);
    }
}
