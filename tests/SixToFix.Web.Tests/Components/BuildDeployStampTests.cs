using System.Security.Claims;
using SixToFix.Application.Services;

namespace SixToFix.Web.Tests.Components;

public sealed class BuildDeployStampTests
{
    private static readonly DateTimeOffset FixedBuild  = new(2026, 5, 20, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FixedDeploy = new(2026, 5, 20, 21, 0, 0, TimeSpan.Zero);

    private static TestContext BuildContext(
        string? role,
        DeploymentInfo? info = null)
    {
        var ctx = new TestContext();
        var auth = ctx.AddTestAuthorization();
        auth.SetAuthorized("testuser");
        auth.SetClaims(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        if (role is not null) auth.SetRoles(role);

        var mock = new Mock<IDeploymentInfoService>();
        mock.Setup(s => s.GetDeploymentInfo())
            .Returns(info ?? new DeploymentInfo(FixedBuild, FixedDeploy, "abc1234"));
        ctx.Services.AddSingleton(mock.Object);

        return ctx;
    }

    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData("TenantAdmin")]
    public void BuildDeployStamp_RendersForAdminRoles(string role)
    {
        using var ctx = BuildContext(role);
        var cut = ctx.RenderComponent<BuildDeployStamp>();

        cut.Markup.Should().Contain("build-deploy-stamp");
        cut.Markup.Should().Contain("Build");
        cut.Markup.Should().Contain("Deploy");
    }

    [Theory]
    [InlineData("Client")]
    [InlineData("Viewer")]
    public void BuildDeployStamp_HiddenForNonAdminRoles(string role)
    {
        using var ctx = BuildContext(role);
        var cut = ctx.RenderComponent<BuildDeployStamp>();

        cut.Markup.Should().NotContain("build-deploy-stamp");
    }

    [Fact]
    public void BuildDeployStamp_ShowsCommitSha()
    {
        using var ctx = BuildContext("SuperAdmin");
        var cut = ctx.RenderComponent<BuildDeployStamp>();

        cut.Markup.Should().Contain("SHA");
        cut.Markup.Should().Contain("abc1234");
    }

    [Fact]
    public void BuildDeployStamp_HidesWhenAllFieldsNull()
    {
        using var ctx = BuildContext("SuperAdmin", new DeploymentInfo(null, null, null));
        var cut = ctx.RenderComponent<BuildDeployStamp>();

        cut.Markup.Should().NotContain("build-deploy-stamp");
    }

    [Fact]
    public void BuildDeployStamp_TooltipContainsUtc()
    {
        using var ctx = BuildContext("SuperAdmin",
            new DeploymentInfo(FixedBuild, FixedDeploy, null));
        var cut = ctx.RenderComponent<BuildDeployStamp>();

        // <time title="..."> must contain formatted UTC
        cut.Markup.Should().Contain("2026-05-20 18:00:00 UTC");
        cut.Markup.Should().Contain("2026-05-20 21:00:00 UTC");
    }

    [Fact]
    public void BuildDeployStamp_TimeElementsHaveIsoDatetime()
    {
        using var ctx = BuildContext("SuperAdmin",
            new DeploymentInfo(FixedBuild, FixedDeploy, null));
        var cut = ctx.RenderComponent<BuildDeployStamp>();

        var times = cut.FindAll("time");
        times.Should().HaveCount(2);
        times[0].GetAttribute("datetime").Should().StartWith("2026-05-20T18:00:00");
        times[1].GetAttribute("datetime").Should().StartWith("2026-05-20T21:00:00");
    }

    // ── Unit tests for RelativeTimeFormatter ──────────────────────────────────
    // ToRelative uses DateTimeOffset.UtcNow internally.  Use inputs that are
    // comfortably inside their bucket so wall-clock drift never flips the result.

    [Fact]
    public void ToRelative_ReturnsJustNow_ForRecentTimestamp()
    {
        var result = RelativeTimeFormatter.ToRelative(DateTimeOffset.UtcNow.AddSeconds(-10));
        result.Should().Be("just now");
    }

    [Fact]
    public void ToRelative_ReturnsMinsAgo_ForMiddleOfMinuteBucket()
    {
        // 30 minutes ago — well inside the 1–59 m bucket
        var result = RelativeTimeFormatter.ToRelative(DateTimeOffset.UtcNow.AddMinutes(-30));
        result.Should().Be("30m ago");
    }

    [Fact]
    public void ToRelative_ReturnsHoursAgo_ForMiddleOfHourBucket()
    {
        // 3 hours ago — well inside the 1–23 h bucket
        var result = RelativeTimeFormatter.ToRelative(DateTimeOffset.UtcNow.AddHours(-3));
        result.Should().Be("3h ago");
    }

    [Fact]
    public void ToRelative_ReturnsDaysAgo_ForMiddleOfDayBucket()
    {
        // 4 days ago — well inside the 1–6 d bucket
        var result = RelativeTimeFormatter.ToRelative(DateTimeOffset.UtcNow.AddDays(-4));
        result.Should().Be("4d ago");
    }

    [Fact]
    public void ToRelative_ReturnsFormattedDate_WhenOlderThanOneWeek()
    {
        var old = DateTimeOffset.UtcNow.AddDays(-30);
        var result = RelativeTimeFormatter.ToRelative(old);
        // Should be a date like "Apr 21, 2026" — not a relative label.
        result.Should().MatchRegex(@"^[A-Z][a-z]+ \d{1,2}, \d{4}$");
    }
}
