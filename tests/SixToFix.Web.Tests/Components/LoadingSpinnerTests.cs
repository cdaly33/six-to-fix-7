namespace SixToFix.Web.Tests.Components;

public sealed class LoadingSpinnerTests
{
    [Fact]
    public void LoadingSpinner_RendersWhenLoading()
    {
        using var ctx = new TestContext();

        var cut = ctx.RenderComponent<LoadingSpinner>(parameters => parameters
            .Add(p => p.IsLoading, true)
            .Add(p => p.Label, "Working..."));

        cut.Find("[role='status']").GetAttribute("aria-label").Should().Be("Working...");
        cut.Markup.Should().Contain("Working...");
    }

    [Fact]
    public void LoadingSpinner_HidesWhenNotLoading()
    {
        using var ctx = new TestContext();

        var cut = ctx.RenderComponent<LoadingSpinner>(parameters => parameters.Add(p => p.IsLoading, false));

        cut.Markup.Trim().Should().BeEmpty();
    }
}
