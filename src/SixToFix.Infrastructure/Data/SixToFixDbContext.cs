using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using SixToFix.Application.Multitenancy;
using SixToFix.Infrastructure.Auth;

namespace SixToFix.Infrastructure.Data;

public sealed class SixToFixDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly ITenantContext _tenantContext;

    public SixToFixDbContext(
        DbContextOptions<SixToFixDbContext> options,
        ITenantContext tenantContext) : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Audit> Audits => Set<Audit>();
    public DbSet<AuditRun> AuditRuns => Set<AuditRun>();
    public DbSet<CategoryConfig> CategoryConfigs => Set<CategoryConfig>();
    public DbSet<SkillRun> SkillRuns => Set<SkillRun>();
    public DbSet<CategoryResult> CategoryResults => Set<CategoryResult>();
    public DbSet<CategoryResultVersion> CategoryResultVersions => Set<CategoryResultVersion>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<PolicyFlag> PolicyFlags => Set<PolicyFlag>();
    public DbSet<CouncilSession> CouncilSessions => Set<CouncilSession>();
    public DbSet<HubSpotSyncQueue> HubSpotSyncQueue => Set<HubSpotSyncQueue>();
    public DbSet<BlobReference> BlobReferences => Set<BlobReference>();
    public DbSet<ReviewerLockout> ReviewerLockouts => Set<ReviewerLockout>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(SixToFixDbContext).Assembly);

        // Global query filters — tenant isolation.
        // IsResolved guard ensures unrestricted access when tenant context is not set
        // (background jobs, migrations, SuperAdmin paths).
        builder.Entity<Client>().HasQueryFilter(e => !_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId);
        builder.Entity<Audit>().HasQueryFilter(e => !_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId);
        builder.Entity<AuditRun>().HasQueryFilter(e => !_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId);
        builder.Entity<CategoryConfig>().HasQueryFilter(e => !_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId);
        builder.Entity<SkillRun>().HasQueryFilter(e => !_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId);
        builder.Entity<CategoryResult>().HasQueryFilter(e => !_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId);
        builder.Entity<CategoryResultVersion>().HasQueryFilter(e => !_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId);
        builder.Entity<Policy>().HasQueryFilter(e => !_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId);
        builder.Entity<PolicyFlag>().HasQueryFilter(e => !_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId);
        builder.Entity<CouncilSession>().HasQueryFilter(e => !_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId);
        builder.Entity<HubSpotSyncQueue>().HasQueryFilter(e => !_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId);
        builder.Entity<BlobReference>().HasQueryFilter(e => !_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId);
        builder.Entity<ReviewerLockout>().HasQueryFilter(e => !_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId);
    }
}
