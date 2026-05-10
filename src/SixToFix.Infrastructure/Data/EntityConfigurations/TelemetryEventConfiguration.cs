using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SixToFix.Infrastructure.Data.EntityConfigurations;

public sealed class TelemetryEventConfiguration : IEntityTypeConfiguration<TelemetryEvent>
{
    public void Configure(EntityTypeBuilder<TelemetryEvent> builder)
    {
        builder.ToTable("telemetry_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.AuditRunId).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.Id });
        builder.HasIndex(e => e.AuditRunId).IsUnique();
        builder.Property(e => e.InitializedAt).HasDefaultValueSql("now()");

        builder.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
        builder.HasOne(e => e.AuditRun).WithMany().HasForeignKey(e => e.AuditRunId);
    }
}
