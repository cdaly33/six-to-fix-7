namespace SixToFix.Web.Tests.Pages;

public sealed class PillarPageTests
{
    private static TestContext BuildContext(string route = "/brand")
    {
        var ctx = new TestContext();
        var auth = ctx.AddTestAuthorization();
        auth.SetAuthorized("testuser");

        var progressMock = new Mock<IProgressService>();
        progressMock.Setup(s => s.GetForUserPillarAsync(It.IsAny<Guid>(), It.IsAny<Pillar>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserPillarProgress?)null);
        progressMock.Setup(s => s.SetPercentAsync(It.IsAny<Guid>(), It.IsAny<Pillar>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserPillarProgress { PercentComplete = 100 });

        var pillarMock = new Mock<IPillarContentService>();
        pillarMock.Setup(s => s.GetForTenantAsync(It.IsAny<Guid>(), It.IsAny<Pillar>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PillarContent?)null);

        ctx.Services.AddSingleton(progressMock.Object);
        ctx.Services.AddSingleton(pillarMock.Object);

        ctx.Services.GetRequiredService<NavigationManager>()
            .NavigateTo($"http://localhost{route}");

        return ctx;
    }

    [Fact]
    public void PillarPage_Brand_ShowsPillarHeading()
    {
        using var ctx = BuildContext("/brand");
        var cut = ctx.RenderComponent<PillarPage>();
        cut.Markup.Should().Contain("Brand Strategy");
    }

    [Fact]
    public void PillarPage_Brand_ShowsPillarOrdinal()
    {
        using var ctx = BuildContext("/brand");
        var cut = ctx.RenderComponent<PillarPage>();
        cut.Markup.Should().Contain("PILLAR 1 OF 6");
    }

    [Fact]
    public void PillarPage_ShowsFiveTabs()
    {
        using var ctx = BuildContext("/brand");
        var cut = ctx.RenderComponent<PillarPage>();
        cut.Markup.Should().Contain("Strategy");
        cut.Markup.Should().Contain("Execution Blueprint");
        cut.Markup.Should().Contain("Templates");
        cut.Markup.Should().Contain("Examples");
        cut.Markup.Should().Contain("Metrics");
    }

    [Fact]
    public void PillarPage_ShowsMarkProgressButtons()
    {
        using var ctx = BuildContext("/brand");
        var cut = ctx.RenderComponent<PillarPage>();
        cut.Markup.Should().Contain("25%");
        cut.Markup.Should().Contain("50%");
        cut.Markup.Should().Contain("75%");
        cut.Markup.Should().Contain("100%");
    }

    [Fact]
    public void PillarPage_Customer_ShowsCorrectOrdinal()
    {
        using var ctx = BuildContext("/customer");
        var cut = ctx.RenderComponent<PillarPage>();
        cut.Markup.Should().Contain("Customer Strategy");
        cut.Markup.Should().Contain("PILLAR 2 OF 6");
    }

    [Fact]
    public void PillarPage_Management_ShowsCorrectOrdinal()
    {
        using var ctx = BuildContext("/management");
        var cut = ctx.RenderComponent<PillarPage>();
        cut.Markup.Should().Contain("Management Strategy");
        cut.Markup.Should().Contain("PILLAR 6 OF 6");
    }

    [Fact]
    public void PillarPage_ShowsEmptyStateWhenNoContent()
    {
        using var ctx = BuildContext("/brand");
        var cut = ctx.RenderComponent<PillarPage>();
        // With no DB content, EmptyContentMessage should render
        cut.Markup.Should().Contain("No content yet");
    }
}
