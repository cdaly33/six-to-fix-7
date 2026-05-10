using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SixToFix.Infrastructure.Data.EntityConfigurations;

public sealed class CalibrationDeltaConfiguration : IEntityTypeConfiguration<CalibrationDelta>
{
    public void Configure(EntityTypeBuilder<CalibrationDelta> builder)
    {
        builder.ToTable("calibration_deltas");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.AuditRunId).IsRequired();
        builder.Property(e => e.CategoryId).IsRequired().HasMaxLength(50);
        builder.Property(e => e.OverrideReasonCode).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Notes).IsRequired().HasMaxLength(2000);
        builder.Property(e => e.OriginalActivityScore).HasPrecision(5, 2);
        builder.Property(e => e.AdjustedActivityScore).HasPrecision(5, 2);
        builder.HasIndex(e => new { e.TenantId, e.Id });
        builder.HasIndex(e => e.AuditRunId);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        builder.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
        builder.HasOne(e => e.AuditRun).WithMany().HasForeignKey(e => e.AuditRunId);
    }
}
