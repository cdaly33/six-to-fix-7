namespace SixToFix.E2E.Tests;

public sealed class LoginFlowTests
{
    [Fact(Skip = "Set PLAYWRIGHT_BASE_URL, E2E_USERNAME, and E2E_PASSWORD to run against a deployed environment.")]
    [Trait("Category", "E2E")]
    public async Task Login_RedirectsToDashboard()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();

        await page.GotoAsync($"{TestSeeder.BaseUrl}/login");
        await page.GetByLabel("Email").FillAsync(TestSeeder.Username);
        await page.GetByLabel("Password").FillAsync(TestSeeder.Password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign In" }).ClickAsync();
        await page.WaitForURLAsync("**/dashboard");

        page.Url.Should().Contain("dashboard");
        (await page.TextContentAsync("h1")).Should().Contain("Dashboard");
    }
}
