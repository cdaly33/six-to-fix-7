using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SixToFix.Domain.Entities;

namespace SixToFix.Infrastructure.Data.EntityConfigurations;

public sealed class ReviewerLockoutConfiguration : IEntityTypeConfiguration<ReviewerLockout>
{
    public void Configure(EntityTypeBuilder<ReviewerLockout> builder)
    {
        builder.ToTable("reviewer_lockouts");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Category).HasMaxLength(50).IsRequired();
        builder.HasIndex(r => r.TenantId);
        builder.HasIndex(r => r.AuditRunId);
        builder.HasIndex(r => new { r.TenantId, r.AuditRunId, r.Category, r.ReviewerUserId }).IsUnique();
        builder.Property(r => r.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(r => r.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasOne(r => r.Tenant)
            .WithMany()
            .HasForeignKey(r => r.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.AuditRun)
            .WithMany()
            .HasForeignKey(r => r.AuditRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
