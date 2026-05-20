using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SixToFix.Application.Multitenancy;
using SixToFix.Domain.Entities;
using SixToFix.Infrastructure.Auth;
using SixToFix.Infrastructure.Data;
using SixToFix.Infrastructure.Services;
using Xunit;

namespace SixToFix.Infrastructure.Tests.Services;

public sealed class TenantServiceTests
{
    [Fact]
    public async Task GetCurrentTenantAsync_ActiveTenant_ReturnsDto()
    {
        await using var db = CreateDbContext();
        var userManager = CreateFakeUserManager();
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Test Tenant",
            Slug = "test-tenant",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        var sut = new TenantService(db, userManager, NullLogger<TenantService>.Instance);

        var result = await sut.GetCurrentTenantAsync(tenantId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(tenantId);
        result.Name.Should().Be("Test Tenant");
        result.Slug.Should().Be("test-tenant");
        result.CreatedAt.Should().Be(now);
    }

    [Fact]
    public async Task GetCurrentTenantAsync_InactiveTenant_ReturnsNull()
    {
        await using var db = CreateDbContext();
        var userManager = CreateFakeUserManager();
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Inactive Tenant",
            Slug = "inactive-tenant",
            IsActive = false,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        var sut = new TenantService(db, userManager, NullLogger<TenantService>.Instance);

        var result = await sut.GetCurrentTenantAsync(tenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateTenantNameAsync_ValidName_UpdatesAndReturnsDto()
    {
        await using var db = CreateDbContext();
        var userManager = CreateFakeUserManager();
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Old Name",
            Slug = "test-tenant",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        var sut = new TenantService(db, userManager, NullLogger<TenantService>.Instance);

        var result = await sut.UpdateTenantNameAsync(tenantId, " New Name ");

        result.Should().NotBeNull();
        result!.Name.Should().Be("New Name");
        
        var stored = await db.Tenants.FindAsync(tenantId);
        stored!.Name.Should().Be("New Name");
        stored.UpdatedAt.Should().BeAfter(now);
    }

    [Fact]
    public async Task UpdateTenantNameAsync_EmptyName_ThrowsInvalidOperationException()
    {
        await using var db = CreateDbContext();
        var userManager = CreateFakeUserManager();
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Test Tenant",
            Slug = "test-tenant",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        var sut = new TenantService(db, userManager, NullLogger<TenantService>.Instance);

        var action = async () => await sut.UpdateTenantNameAsync(tenantId, "  ");

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Tenant name cannot be empty.");
    }

    [Fact]
    public async Task GetTenantUsersAsync_ReturnsUsersWithRoles()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        var user1 = new ApplicationUser
        {
            Id = user1Id,
            TenantId = tenantId,
            UserName = "admin@test.com",
            Email = "admin@test.com",
            FullName = "Admin User",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        var user2 = new ApplicationUser
        {
            Id = user2Id,
            TenantId = tenantId,
            UserName = "viewer@test.com",
            Email = "viewer@test.com",
            FullName = "Viewer User",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Users.AddRange(user1, user2);

        var role1 = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "TenantAdmin", NormalizedName = "TENANTADMIN" };
        var role2 = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Viewer", NormalizedName = "VIEWER" };
        db.Roles.AddRange(role1, role2);

        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user1Id, RoleId = role1.Id });
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user2Id, RoleId = role2.Id });

        await db.SaveChangesAsync();

        var userManager = CreateFakeUserManager(db);

        var sut = new TenantService(db, userManager, NullLogger<TenantService>.Instance);

        var result = await sut.GetTenantUsersAsync(tenantId);

        result.Should().HaveCount(2);
        result[0].Email.Should().Be("admin@test.com");
        result[0].Role.Should().Be("TenantAdmin");
        result[1].Email.Should().Be("viewer@test.com");
        result[1].Role.Should().Be("Viewer");
    }

    private static SixToFixDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SixToFixDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new SixToFixDbContext(options, new TestTenantContext());
    }

    private static FakeUserManager CreateFakeUserManager(SixToFixDbContext? db = null)
    {
        return new FakeUserManager(db);
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public string TenantSlug => string.Empty;
        public bool IsResolved => false;
    }

    private sealed class FakeUserManager : UserManager<ApplicationUser>
    {
        private readonly SixToFixDbContext? _db;

        public FakeUserManager(SixToFixDbContext? db = null)
            : base(new FakeUserStore(), null, null, null, null, null, null, null, null)
        {
            _db = db;
        }

        public override async Task<IList<string>> GetRolesAsync(ApplicationUser user)
        {
            if (_db is null)
                return new List<string> { "Viewer" };

            var roleIds = await _db.UserRoles
                .Where(ur => ur.UserId == user.Id)
                .Select(ur => ur.RoleId)
                .ToListAsync();

            var roles = await _db.Roles
                .Where(r => roleIds.Contains(r.Id))
                .Select(r => r.Name!)
                .ToListAsync();

            return roles;
        }

        private sealed class FakeUserStore : IUserStore<ApplicationUser>
        {
            public void Dispose() { }
            public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.Id.ToString());
            public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.UserName);
            public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken) { user.UserName = userName; return Task.CompletedTask; }
            public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedUserName);
            public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken) { user.NormalizedUserName = normalizedName; return Task.CompletedTask; }
            public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
            public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
            public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
            public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<ApplicationUser?>(null);
            public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => Task.FromResult<ApplicationUser?>(null);
        }
    }
}
