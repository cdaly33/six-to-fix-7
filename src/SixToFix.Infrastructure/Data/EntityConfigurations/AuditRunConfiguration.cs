using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SixToFix.Domain.Entities;

namespace SixToFix.Infrastructure.Data.EntityConfigurations;

public sealed class AuditRunConfiguration : IEntityTypeConfiguration<AuditRun>
{
    public void Configure(EntityTypeBuilder<AuditRun> builder)
    {
        builder.ToTable("audit_runs");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Status).HasMaxLength(20).IsRequired();
        builder.Property(r => r.ErrorMessage).HasColumnType("text");
        builder.Property(r => r.Tier).HasMaxLength(10);
        builder.Property(r => r.SystemsMaturityScore).HasPrecision(5, 2);
        builder.Property(r => r.AiReadinessScore).HasPrecision(5, 2);
        builder.HasIndex(r => r.TenantId);
        builder.HasIndex(r => r.AuditId);
        builder.Property(r => r.CreatedAt).HasDefaultValueSql("now()");

        builder.HasOne(r => r.Tenant)
            .WithMany()
            .HasForeignKey(r => r.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Audit)
            .WithMany(a => a.AuditRuns)
            .HasForeignKey(r => r.AuditId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
