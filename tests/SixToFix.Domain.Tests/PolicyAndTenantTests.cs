using SixToFix.Domain.Entities;
using Xunit;

namespace SixToFix.Domain.Tests;

/// <summary>
/// Unit tests for Policy and Tenant entities to restore domain coverage.
/// </summary>
public sealed class PolicyAndTenantTests
{
    // ── Policy entity ─────────────────────────────────────────────────────────

    [Fact]
    public void Policy_DefaultValues_AreCorrect()
    {
        var policy = new Policy();

        Assert.Equal(Guid.Empty, policy.Id);
        Assert.Equal(Guid.Empty, policy.TenantId);
        Assert.Equal(string.Empty, policy.RuleCode);
        Assert.Equal("Warning", policy.Severity);
        Assert.True(policy.IsEnabled);
        Assert.Null(policy.ConfigJson);
    }

    [Fact]
    public void Policy_PropertyAssignment_RoundTrips()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var tenant = new Tenant { Id = tenantId, Name = "Acme" };

        var policy = new Policy
        {
            Id = id,
            TenantId = tenantId,
            RuleCode = "REQUIRE_LOGO",
            Severity = "Error",
            IsEnabled = false,
            ConfigJson = """{"threshold":5}""",
            CreatedAt = now,
            UpdatedAt = now,
            Tenant = tenant
        };

        Assert.Equal(id, policy.Id);
        Assert.Equal(tenantId, policy.TenantId);
        Assert.Equal("REQUIRE_LOGO", policy.RuleCode);
        Assert.Equal("Error", policy.Severity);
        Assert.False(policy.IsEnabled);
        Assert.Equal("""{"threshold":5}""", policy.ConfigJson);
        Assert.Equal(now, policy.CreatedAt);
        Assert.Equal(now, policy.UpdatedAt);
        Assert.Same(tenant, policy.Tenant);
    }

    [Theory]
    [InlineData("Warning")]
    [InlineData("Error")]
    [InlineData("Info")]
    public void Policy_Severity_CanBeSetToAnyString(string severity)
    {
        var policy = new Policy { Severity = severity };
        Assert.Equal(severity, policy.Severity);
    }

    [Fact]
    public void Policy_IsEnabled_CanBeToggled()
    {
        var policy = new Policy();
        Assert.True(policy.IsEnabled);

        policy.IsEnabled = false;
        Assert.False(policy.IsEnabled);

        policy.IsEnabled = true;
        Assert.True(policy.IsEnabled);
    }

    [Fact]
    public void Policy_ConfigJson_CanBeNull()
    {
        var policy = new Policy { ConfigJson = null };
        Assert.Null(policy.ConfigJson);
    }

    [Fact]
    public void Policy_ConfigJson_CanBeSet()
    {
        var policy = new Policy { ConfigJson = "{}" };
        Assert.Equal("{}", policy.ConfigJson);
    }

    // ── Tenant entity ─────────────────────────────────────────────────────────

    [Fact]
    public void Tenant_DefaultValues_AreCorrect()
    {
        var tenant = new Tenant();

        Assert.Equal(Guid.Empty, tenant.Id);
        Assert.Equal(string.Empty, tenant.Name);
        Assert.Equal(string.Empty, tenant.Slug);
        Assert.Null(tenant.LogoUrl);
        Assert.True(tenant.IsActive);
    }

    [Fact]
    public void Tenant_PropertyAssignment_RoundTrips()
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var tenant = new Tenant
        {
            Id = id,
            Name = "Acme Corp",
            Slug = "acme-corp",
            LogoUrl = "https://cdn.example.com/logo.png",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        Assert.Equal(id, tenant.Id);
        Assert.Equal("Acme Corp", tenant.Name);
        Assert.Equal("acme-corp", tenant.Slug);
        Assert.Equal("https://cdn.example.com/logo.png", tenant.LogoUrl);
        Assert.True(tenant.IsActive);
        Assert.Equal(now, tenant.CreatedAt);
        Assert.Equal(now, tenant.UpdatedAt);
    }

    [Fact]
    public void Tenant_LogoUrl_CanBeNull()
    {
        var tenant = new Tenant { LogoUrl = null };
        Assert.Null(tenant.LogoUrl);
    }

    [Fact]
    public void Tenant_IsActive_CanBeSetFalse()
    {
        var tenant = new Tenant { IsActive = false };
        Assert.False(tenant.IsActive);
    }
}
