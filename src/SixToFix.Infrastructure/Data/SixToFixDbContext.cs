using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using SixToFix.Application.Multitenancy;
using SixToFix.Infrastructure.Auth;

namespace SixToFix.Infrastructure.Data;

public sealed class SixToFixDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly ITenantContext _tenantContext;
    public SixToFixDbContext(DbContextOptions<SixToFixDbContext> options, ITenantContext tenantContext) : base(options) => _tenantContext = tenantContext;
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<PillarContent> PillarContents => Set<PillarContent>();
    public DbSet<UserPillarProgress> UserPillarProgresses => Set<UserPillarProgress>();
    public DbSet<PlaybookTemplate> PlaybookTemplates => Set<PlaybookTemplate>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(SixToFixDbContext).Assembly);
        builder.Entity<PillarContent>().HasQueryFilter(e => !_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId);
        builder.Entity<UserPillarProgress>().HasQueryFilter(e => !_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId);
        builder.Entity<PlaybookTemplate>().HasQueryFilter(e => !_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId);
    }
}
