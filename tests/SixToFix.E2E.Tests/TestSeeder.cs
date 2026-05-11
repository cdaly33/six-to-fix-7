namespace SixToFix.E2E.Tests;

public static class TestSeeder
{
    public static string BaseUrl => Environment.GetEnvironmentVariable("PLAYWRIGHT_BASE_URL") ?? "https://example.invalid";
    public static string Username => Environment.GetEnvironmentVariable("E2E_USERNAME") ?? "seeded-user@example.com";
    public static string Password => Environment.GetEnvironmentVariable("E2E_PASSWORD") ?? "Password123!";
}
