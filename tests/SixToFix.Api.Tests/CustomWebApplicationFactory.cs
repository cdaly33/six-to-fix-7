using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace SixToFix.Api.Tests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string JwtIssuer = "https://tests.strategicglue.local";
    private const string JwtAudience = "six-to-fix-tests";
    private const string JwtSigningKey = "super-secret-integration-key-1234567890";

    public Mock<IAuthService> AuthService { get; } = new();
    public Mock<IAuditOrchestrator> AuditOrchestrator { get; } = new();
    public Mock<IReviewerWorkflow> ReviewerWorkflow { get; } = new();
    public Mock<IPublisher> Publisher { get; } = new();
    public Mock<ICalibrationTracker> CalibrationTracker { get; } = new();
    public Mock<IHubSpotClient> HubSpotClient { get; } = new();
    public Channel<HubSpotEvent> WebhookEvents { get; } = Channel.CreateUnbounded<HubSpotEvent>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=sixtofix-tests;Username=test;Password=test",
                ["Jwt:Issuer"] = JwtIssuer,
                ["Jwt:Audience"] = JwtAudience,
                ["Jwt:SigningKey"] = JwtSigningKey,
                ["Search:Endpoint"] = "https://search-strategicglue-dev.search.windows.net",
                ["Storage:BlobEndpoint"] = "https://storage-strategicglue-dev.blob.core.windows.net/",
                ["HubSpot:PrivateAppToken"] = "test-token"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IAuthService>();
            services.RemoveAll<IAuditOrchestrator>();
            services.RemoveAll<IReviewerWorkflow>();
            services.RemoveAll<IPublisher>();
            services.RemoveAll<ICalibrationTracker>();
            services.RemoveAll<IHubSpotClient>();
            services.RemoveAll<Channel<HubSpotEvent>>();

            services.AddSingleton(AuthService.Object);
            services.AddSingleton(AuditOrchestrator.Object);
            services.AddSingleton(ReviewerWorkflow.Object);
            services.AddSingleton(Publisher.Object);
            services.AddSingleton(CalibrationTracker.Object);
            services.AddSingleton(HubSpotClient.Object);
            services.AddSingleton(WebhookEvents);
        });
    }

    public HttpClient CreateAuthenticatedClient(params string[] roles)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(roles));
        return client;
    }

    private static string CreateToken(IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new("tenant_id", Guid.NewGuid().ToString())
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
