using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SixToFix.Domain.Entities;

namespace SixToFix.Infrastructure.Data.EntityConfigurations;

public sealed class CategoryConfigConfiguration : IEntityTypeConfiguration<CategoryConfig>
{
    public void Configure(EntityTypeBuilder<CategoryConfig> builder)
    {
        builder.ToTable("category_configs");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Category).HasMaxLength(30).IsRequired();
        builder.Property(c => c.CustomPromptOverride).HasColumnType("text");
        builder.HasIndex(c => c.TenantId);
        builder.HasIndex(c => c.AuditId);
        builder.HasIndex(c => new { c.AuditId, c.Category }).IsUnique();
        builder.Property(c => c.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(c => c.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasOne(c => c.Tenant)
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Audit)
            .WithMany(a => a.CategoryConfigs)
            .HasForeignKey(c => c.AuditId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
