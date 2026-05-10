using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SixToFix.Domain.Entities;

namespace SixToFix.Infrastructure.Data.EntityConfigurations;

public sealed class BlobReferenceConfiguration : IEntityTypeConfiguration<BlobReference>
{
    public void Configure(EntityTypeBuilder<BlobReference> builder)
    {
        builder.ToTable("blob_references");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.ContainerName).HasMaxLength(100).IsRequired();
        builder.Property(b => b.BlobName).HasMaxLength(500).IsRequired();
        builder.Property(b => b.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(b => b.LinkedEntityType).HasMaxLength(100);
        builder.HasIndex(b => b.TenantId);
        builder.HasIndex(b => new { b.LinkedEntityType, b.LinkedEntityId });
        builder.Property(b => b.CreatedAt).HasDefaultValueSql("now()");

        builder.HasOne(b => b.Tenant)
            .WithMany()
            .HasForeignKey(b => b.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
