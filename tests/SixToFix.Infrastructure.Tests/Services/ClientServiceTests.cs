using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SixToFix.Application.Models;
using SixToFix.Infrastructure.Data;
using SixToFix.Infrastructure.Services;
using Xunit;

namespace SixToFix.Infrastructure.Tests.Services;

public sealed class ClientServiceTests
{
    [Fact]
    public async Task CreateAsync_NewClient_PersistsAndReturnsId()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var sut = new ClientService(db, NullLogger<ClientService>.Instance);

        var id = await sut.CreateAsync(new CreateClientDto
        {
            Name = " Acme Co ",
            ContactEmail = " hello@example.com ",
            Notes = " First client "
        }, tenantId);

        id.Should().NotBe(Guid.Empty);
        var stored = await db.Clients.IgnoreQueryFilters().SingleAsync(e => e.Id == id);
        stored.TenantId.Should().Be(tenantId);
        stored.Name.Should().Be("Acme Co");
        stored.ContactEmail.Should().Be("hello@example.com");
        stored.Notes.Should().Be("First client");
        stored.IsActive.Should().BeTrue();
        stored.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        stored.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateAsync_DuplicateActiveNameForTenant_ThrowsInvalidOperationException()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var sut = new ClientService(db, NullLogger<ClientService>.Instance);

        await sut.CreateAsync(new CreateClientDto { Name = "Acme Co" }, tenantId);

        var action = async () => await sut.CreateAsync(new CreateClientDto { Name = " acme co " }, tenantId);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("A client with this name already exists for the tenant.");
    }

    [Fact]
    public async Task GetAllForTenantAsync_ReturnsOnlyActiveClientsForTenant()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        db.Clients.AddRange(
            new Client { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Beta", IsActive = true, CreatedAt = now, UpdatedAt = now },
            new Client { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Alpha", IsActive = true, CreatedAt = now, UpdatedAt = now },
            new Client { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Deleted", IsActive = false, CreatedAt = now, UpdatedAt = now },
            new Client { Id = Guid.NewGuid(), TenantId = otherTenantId, Name = "Other", IsActive = true, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();
        var sut = new ClientService(db, NullLogger<ClientService>.Instance);

        var result = await sut.GetAllForTenantAsync(tenantId);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(e => e.TenantId == tenantId && e.IsActive);
        result.Select(e => e.Name).Should().Equal("Alpha", "Beta");
    }

    private static SixToFixDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SixToFixDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new SixToFixDbContext(options, new TestTenantContext());
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public string TenantSlug => string.Empty;
        public bool IsResolved => false;
    }
}
