namespace SixToFix.Web.Tests.Components;

public sealed class TierBadgeTests
{
    [Theory]
    [InlineData("tier_1", "tier-1", "Tier 1")]
    [InlineData("tier_2", "tier-2", "Tier 2")]
    [InlineData("tier_3", "tier-3", "Tier 3")]
    public void TierBadge_RendersExpectedTierPresentation(string tier, string expectedClass, string expectedLabel)
    {
        using var ctx = new TestContext();

        var cut = ctx.RenderComponent<TierBadge>(parameters => parameters.Add(p => p.Tier, tier));

        cut.Find("span").ClassList.Should().Contain(expectedClass);
        cut.Markup.Should().Contain(expectedLabel);
    }
}
