using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SixToFix.Infrastructure.Data.EntityConfigurations;

public sealed class ReviewerActionConfiguration : IEntityTypeConfiguration<ReviewerAction>
{
    public void Configure(EntityTypeBuilder<ReviewerAction> builder)
    {
        builder.ToTable("reviewer_actions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.AuditRunId).IsRequired();
        builder.Property(e => e.CategoryId).IsRequired().HasMaxLength(50);
        builder.Property(e => e.ActionType).IsRequired().HasMaxLength(50);
        builder.HasIndex(e => new { e.TenantId, e.Id });
        builder.HasIndex(e => e.AuditRunId);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        builder.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
        builder.HasOne(e => e.AuditRun).WithMany().HasForeignKey(e => e.AuditRunId);
    }
}
