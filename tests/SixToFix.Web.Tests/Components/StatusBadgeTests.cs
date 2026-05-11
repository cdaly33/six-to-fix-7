namespace SixToFix.Web.Tests.Components;

public sealed class StatusBadgeTests
{
    [Theory]
    [InlineData("completed", "badge-success")]
    [InlineData("running", "badge-info")]
    [InlineData("failed", "badge-error")]
    [InlineData("pending", "badge-warning")]
    public void AuditStatusChip_MapsStatusToExpectedCssClass(string status, string expectedClass)
    {
        using var ctx = new TestContext();

        var cut = ctx.RenderComponent<AuditStatusChip>(parameters => parameters.Add(p => p.Status, status));

        cut.Find("span").ClassList.Should().Contain(expectedClass);
        cut.Markup.Should().Contain(status);
    }
}
