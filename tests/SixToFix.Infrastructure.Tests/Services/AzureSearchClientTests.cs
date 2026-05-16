using System.Reflection;
using FluentAssertions;
using SixToFix.Infrastructure.ExternalClients;

namespace SixToFix.Infrastructure.Tests.Services;

public sealed class AzureSearchClientTests
{
    [Fact]
    public void SearchFilter_AlwaysIncludesTenantScope()
    {
        var filter = InvokeBuildFilter("tenant-123", null);

        filter.Should().Be("tenantId eq 'tenant-123'");
    }

    [Fact]
    public void SearchFilter_AppendsAdditionalCriteria_WhenProvided()
    {
        var filter = InvokeBuildFilter("tenant-123", "clientId eq 'client-456' and area eq 'brand'");

        filter.Should().Be("tenantId eq 'tenant-123' and (clientId eq 'client-456' and area eq 'brand')");
    }

    [Fact]
    public void RequiredIndexes_ContainAllProvisionedSearchSchemas()
    {
        var property = typeof(AzureSearchClient).GetProperty("RequiredIndexes", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        property.Should().NotBeNull();

        var indexes = property!.GetValue(null) as IReadOnlyList<string>;
        indexes.Should().BeEquivalentTo([
            "six-to-fix-evidence"
        ]);
    }

    private static string InvokeBuildFilter(string tenantId, string? additionalFilter)
    {
        var method = typeof(AzureSearchClient).GetMethod("BuildFilter", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        method.Should().NotBeNull();

        return (string)method!.Invoke(null, [tenantId, additionalFilter])!;
    }
}
