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
    private readonly Action<Dictionary<string, string?>>? _configureOverrides;
    public Mock<IAuthService> AuthService { get; } = new();
    public CustomWebApplicationFactory(Action<Dictionary<string, string?>>? configureOverrides = null)
    {
        _configureOverrides = configureOverrides;
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=sixtofix-tests;Username=test;Password=test",
                ["Jwt:Issuer"] = JwtIssuer,
                ["Jwt:Audience"] = JwtAudience,
                ["Jwt:SigningKey"] = JwtSigningKey,
                ["Search:Endpoint"] = "https://search-strategicglue-dev.search.windows.net",
                ["Storage:BlobEndpoint"] = "https://storage-strategicglue-dev.blob.core.windows.net/"
            };
            _configureOverrides?.Invoke(values);
            config.AddInMemoryCollection(values);
        });
        builder.ConfigureTestServices(services => { services.RemoveAll<IHostedService>(); services.RemoveAll<IAuthService>(); services.AddSingleton(AuthService.Object); });
    }
    public HttpClient CreateAuthenticatedClient(params string[] roles) { var client = CreateClient(); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(roles)); return client; }
    private static string CreateToken(IEnumerable<string> roles) { var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new("tenant_id", Guid.NewGuid().ToString()) }; claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role))); var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSigningKey)), SecurityAlgorithms.HmacSha256); var token = new JwtSecurityToken(issuer: JwtIssuer, audience: JwtAudience, claims: claims, expires: DateTime.UtcNow.AddHours(1), signingCredentials: credentials); return new JwtSecurityTokenHandler().WriteToken(token); }
}
