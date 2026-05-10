using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SixToFix.Domain.Entities;

namespace SixToFix.Infrastructure.Data.EntityConfigurations;

public sealed class CategoryResultConfiguration : IEntityTypeConfiguration<CategoryResult>
{
    public void Configure(EntityTypeBuilder<CategoryResult> builder)
    {
        builder.ToTable("category_results");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Category).HasMaxLength(30).IsRequired();
        builder.Property(r => r.Status).HasMaxLength(20).IsRequired();
        builder.Property(r => r.SystemsMaturityContribution).HasPrecision(5, 2);
        builder.Property(r => r.ReviewNotes).HasColumnType("text");
        builder.HasIndex(r => r.TenantId);
        builder.HasIndex(r => r.AuditRunId);
        builder.HasIndex(r => new { r.AuditRunId, r.Category }).IsUnique();
        builder.Property(r => r.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(r => r.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasOne(r => r.Tenant)
            .WithMany()
            .HasForeignKey(r => r.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.AuditRun)
            .WithMany(a => a.CategoryResults)
            .HasForeignKey(r => r.AuditRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
