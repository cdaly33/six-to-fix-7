namespace SixToFix.Api.Tests;

public sealed class AuditRunEndpointTests
{
    [Fact]
    public async Task CreateAuditRun_ReturnsCreated_ForTenantAdmin()
    {
        using var factory = new CustomWebApplicationFactory();
        var run = BuildAuditRun();

        factory.AuditOrchestrator
            .Setup(service => service.CreateAuditRunAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        var response = await factory.CreateAuthenticatedClient("TenantAdmin")
            .PostAsJsonAsync("/api/audit-runs", new CreateAuditRunRequest(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAuditRun_ReturnsUnauthorized_WhenCallerIsAnonymous()
    {
        using var factory = new CustomWebApplicationFactory();

        var response = await factory.CreateClient()
            .PostAsJsonAsync("/api/audit-runs", new CreateAuditRunRequest(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateAuditRun_ReturnsForbidden_ForViewerRole()
    {
        using var factory = new CustomWebApplicationFactory();

        var response = await factory.CreateAuthenticatedClient("Viewer")
            .PostAsJsonAsync("/api/audit-runs", new CreateAuditRunRequest(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAuditRun_ReturnsAuditRun_ForAuthenticatedUser()
    {
        using var factory = new CustomWebApplicationFactory();
        var run = BuildAuditRun();

        factory.AuditOrchestrator
            .Setup(service => service.GetAuditRunAsync(run.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        var response = await factory.CreateAuthenticatedClient("Viewer")
            .GetAsync($"/api/audit-runs/{run.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<AuditRun>();
        payload!.Id.Should().Be(run.Id);
    }

    [Fact]
    public async Task ListAuditRuns_ReturnsClientRuns_ForAuthenticatedUser()
    {
        using var factory = new CustomWebApplicationFactory();
        var clientId = Guid.NewGuid();
        var runs = new[] { BuildAuditRun(), BuildAuditRun() };

        factory.AuditOrchestrator
            .Setup(service => service.GetAuditRunsForClientAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);

        var response = await factory.CreateAuthenticatedClient("Reviewer")
            .GetAsync($"/api/audit-runs?clientId={clientId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<List<AuditRun>>();
        payload.Should().HaveCount(2);
    }

    [Fact]
    public async Task StartAuditRun_ReturnsOk_ForTenantAdmin()
    {
        using var factory = new CustomWebApplicationFactory();
        var auditRunId = Guid.NewGuid();

        factory.AuditOrchestrator
            .Setup(service => service.StartAuditRunAsync(auditRunId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await factory.CreateAuthenticatedClient("TenantAdmin")
            .PostAsync($"/api/audit-runs/{auditRunId}/start", JsonContent.Create(new { }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static AuditRun BuildAuditRun() => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        AuditId = Guid.NewGuid(),
        Status = "pending",
        StartedAt = DateTimeOffset.UtcNow,
        CreatedAt = DateTimeOffset.UtcNow
    };
}
