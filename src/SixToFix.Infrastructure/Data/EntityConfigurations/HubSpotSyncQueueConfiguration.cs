using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SixToFix.Domain.Entities;

namespace SixToFix.Infrastructure.Data.EntityConfigurations;

public sealed class HubSpotSyncQueueConfiguration : IEntityTypeConfiguration<HubSpotSyncQueue>
{
    public void Configure(EntityTypeBuilder<HubSpotSyncQueue> builder)
    {
        builder.ToTable("hub_spot_sync_queue");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.EventType).HasMaxLength(100).IsRequired();
        builder.Property(h => h.PayloadJson).HasColumnType("text").IsRequired();
        builder.Property(h => h.Status).HasMaxLength(20).IsRequired();
        builder.Property(h => h.LastErrorMessage).HasColumnType("text");
        builder.HasIndex(h => h.TenantId);
        builder.HasIndex(h => new { h.Status, h.NextRetryAt });
        builder.Property(h => h.CreatedAt).HasDefaultValueSql("now()");

        builder.HasOne(h => h.Tenant)
            .WithMany()
            .HasForeignKey(h => h.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.Client)
            .WithMany()
            .HasForeignKey(h => h.ClientId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
