using System.Text.Json;
using SixToFix.Domain.Constants;
using SixToFix.Domain.Entities;
using SixToFix.Domain.Enums;
using Xunit;

namespace SixToFix.Domain.Tests;

/// <summary>
/// Unit tests for the StrategyHub domain entities and related enums/constants.
/// These drive coverage of PillarContent, UserPillarProgress, PlaybookTemplate,
/// Pillar, PlaybookTemplateStatus, User, and Roles.
/// </summary>
public sealed class StrategyHubTests
{
    // ── Pillar enum ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(Pillar.Brand, 1)]
    [InlineData(Pillar.Customer, 2)]
    [InlineData(Pillar.Offering, 3)]
    [InlineData(Pillar.Communication, 4)]
    [InlineData(Pillar.Sales, 5)]
    [InlineData(Pillar.Management, 6)]
    public void Pillar_Values_AreExpectedIntegers(Pillar pillar, int expected)
    {
        Assert.Equal(expected, (int)pillar);
    }

    [Fact]
    public void Pillar_AllSixValues_Defined()
    {
        var values = Enum.GetValues<Pillar>();
        Assert.Equal(6, values.Length);
    }

    // ── PlaybookTemplateStatus enum ───────────────────────────────────────────

    [Theory]
    [InlineData(PlaybookTemplateStatus.Draft, 0)]
    [InlineData(PlaybookTemplateStatus.Published, 1)]
    [InlineData(PlaybookTemplateStatus.Archived, 2)]
    public void PlaybookTemplateStatus_Values_AreExpectedIntegers(PlaybookTemplateStatus status, int expected)
    {
        Assert.Equal(expected, (int)status);
    }

    [Fact]
    public void PlaybookTemplateStatus_ThreeValues_Defined()
    {
        var values = Enum.GetValues<PlaybookTemplateStatus>();
        Assert.Equal(3, values.Length);
    }

    // ── PillarContent ─────────────────────────────────────────────────────────

    [Fact]
    public void PillarContent_DefaultValues_AreCorrect()
    {
        var content = new PillarContent();

        Assert.Equal(Guid.Empty, content.Id);
        Assert.Equal(Guid.Empty, content.TenantId);
        Assert.Equal(string.Empty, content.Title);
        Assert.Null(content.Subtitle);
        Assert.Equal("{}", content.BodyJson);
        Assert.Null(content.UpdatedByUserId);
    }

    [Fact]
    public void PillarContent_PropertyAssignment_RoundTrips()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var updatedBy = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var content = new PillarContent
        {
            Id = id,
            TenantId = tenantId,
            Pillar = Pillar.Brand,
            Title = "Brand Pillar",
            Subtitle = "Building your brand",
            BodyJson = """{"strategy":[]}""",
            CreatedAt = now,
            UpdatedAt = now,
            UpdatedByUserId = updatedBy
        };

        Assert.Equal(id, content.Id);
        Assert.Equal(tenantId, content.TenantId);
        Assert.Equal(Pillar.Brand, content.Pillar);
        Assert.Equal("Brand Pillar", content.Title);
        Assert.Equal("Building your brand", content.Subtitle);
        Assert.Equal("""{"strategy":[]}""", content.BodyJson);
        Assert.Equal(now, content.CreatedAt);
        Assert.Equal(now, content.UpdatedAt);
        Assert.Equal(updatedBy, content.UpdatedByUserId);
    }

    [Fact]
    public void PillarContent_BodyJson_RoundTripsViaSystemTextJson()
    {
        var body = new { strategy = new[] { new { title = "Awareness", points = new[] { "Social media", "SEO" } } } };
        var json = JsonSerializer.Serialize(body);

        var content = new PillarContent { BodyJson = json };

        var deserialized = JsonSerializer.Deserialize<JsonElement>(content.BodyJson);
        Assert.Equal("Awareness", deserialized.GetProperty("strategy")[0].GetProperty("title").GetString());
    }

    [Fact]
    public void PillarContent_EachPillarValue_CanBeAssigned()
    {
        foreach (var pillar in Enum.GetValues<Pillar>())
        {
            var content = new PillarContent { Pillar = pillar };
            Assert.Equal(pillar, content.Pillar);
        }
    }

    // ── UserPillarProgress ────────────────────────────────────────────────────

    [Fact]
    public void UserPillarProgress_DefaultValues_AreCorrect()
    {
        var progress = new UserPillarProgress();

        Assert.Equal(Guid.Empty, progress.Id);
        Assert.Equal(Guid.Empty, progress.TenantId);
        Assert.Equal(Guid.Empty, progress.UserId);
        Assert.Equal(0, progress.PercentComplete);
    }

    [Fact]
    public void UserPillarProgress_PropertyAssignment_RoundTrips()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var progress = new UserPillarProgress
        {
            Id = id,
            TenantId = tenantId,
            UserId = userId,
            Pillar = Pillar.Customer,
            PercentComplete = 75,
            LastActivityAt = now
        };

        Assert.Equal(id, progress.Id);
        Assert.Equal(tenantId, progress.TenantId);
        Assert.Equal(userId, progress.UserId);
        Assert.Equal(Pillar.Customer, progress.Pillar);
        Assert.Equal(75, progress.PercentComplete);
        Assert.Equal(now, progress.LastActivityAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void UserPillarProgress_PercentComplete_AcceptsBoundaryValues(int percent)
    {
        var progress = new UserPillarProgress { PercentComplete = percent };
        Assert.Equal(percent, progress.PercentComplete);
    }

    [Fact]
    public void UserPillarProgress_EachPillarValue_CanBeAssigned()
    {
        foreach (var pillar in Enum.GetValues<Pillar>())
        {
            var progress = new UserPillarProgress { Pillar = pillar };
            Assert.Equal(pillar, progress.Pillar);
        }
    }

    // ── PlaybookTemplate ──────────────────────────────────────────────────────

    [Fact]
    public void PlaybookTemplate_DefaultValues_AreCorrect()
    {
        var template = new PlaybookTemplate();

        Assert.Equal(Guid.Empty, template.Id);
        Assert.Equal(Guid.Empty, template.TenantId);
        Assert.Null(template.Pillar);
        Assert.Equal(string.Empty, template.Name);
        Assert.Equal(string.Empty, template.Format);
        Assert.Equal(PlaybookTemplateStatus.Draft, template.Status);
        Assert.Equal(0, template.Popularity);
        Assert.Null(template.Notes);
    }

    [Fact]
    public void PlaybookTemplate_PropertyAssignment_RoundTrips()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var template = new PlaybookTemplate
        {
            Id = id,
            TenantId = tenantId,
            Pillar = Pillar.Sales,
            Name = "Sales Playbook",
            Format = "doc",
            Status = PlaybookTemplateStatus.Published,
            Popularity = 42,
            LastUpdatedAt = now,
            Notes = "Reviewed by team"
        };

        Assert.Equal(id, template.Id);
        Assert.Equal(tenantId, template.TenantId);
        Assert.Equal(Pillar.Sales, template.Pillar);
        Assert.Equal("Sales Playbook", template.Name);
        Assert.Equal("doc", template.Format);
        Assert.Equal(PlaybookTemplateStatus.Published, template.Status);
        Assert.Equal(42, template.Popularity);
        Assert.Equal(now, template.LastUpdatedAt);
        Assert.Equal("Reviewed by team", template.Notes);
    }

    [Fact]
    public void PlaybookTemplate_NullPillar_SpansAllPillars()
    {
        var template = new PlaybookTemplate { Pillar = null };
        Assert.Null(template.Pillar);
    }

    [Theory]
    [InlineData(PlaybookTemplateStatus.Draft)]
    [InlineData(PlaybookTemplateStatus.Published)]
    [InlineData(PlaybookTemplateStatus.Archived)]
    public void PlaybookTemplate_AllStatusTransitions_CanBeSet(PlaybookTemplateStatus status)
    {
        var template = new PlaybookTemplate { Status = status };
        Assert.Equal(status, template.Status);
    }

    [Fact]
    public void PlaybookTemplate_FormatVariants_CanBeSet()
    {
        foreach (var fmt in new[] { "doc", "spreadsheet", "kit" })
        {
            var template = new PlaybookTemplate { Format = fmt };
            Assert.Equal(fmt, template.Format);
        }
    }

    // ── User entity ───────────────────────────────────────────────────────────

    [Fact]
    public void User_DefaultValues_AreCorrect()
    {
        var user = new User();

        Assert.Equal(string.Empty, user.Email);
        Assert.Equal(string.Empty, user.FullName);
        Assert.Equal(string.Empty, user.Role);
        Assert.True(user.IsActive);
    }

    [Fact]
    public void User_PropertyAssignment_RoundTrips()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var user = new User
        {
            Id = id,
            TenantId = tenantId,
            Email = "neo@sixtofix.com",
            FullName = "Neo Dev",
            Role = Roles.TenantAdmin,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        Assert.Equal(id, user.Id);
        Assert.Equal(tenantId, user.TenantId);
        Assert.Equal("neo@sixtofix.com", user.Email);
        Assert.Equal("Neo Dev", user.FullName);
        Assert.Equal(Roles.TenantAdmin, user.Role);
        Assert.True(user.IsActive);
        Assert.Equal(now, user.CreatedAt);
        Assert.Equal(now, user.UpdatedAt);
    }

    // ── Roles constants ───────────────────────────────────────────────────────

    [Fact]
    public void Roles_Constants_HaveExpectedValues()
    {
        Assert.Equal("SuperAdmin", Roles.SuperAdmin);
        Assert.Equal("TenantAdmin", Roles.TenantAdmin);
        Assert.Equal("Client", Roles.Client);
    }
}
