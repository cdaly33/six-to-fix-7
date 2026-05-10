using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SixToFix.Domain.Entities;

namespace SixToFix.Infrastructure.Data.EntityConfigurations;

public sealed class PolicyConfiguration : IEntityTypeConfiguration<Policy>
{
    public void Configure(EntityTypeBuilder<Policy> builder)
    {
        builder.ToTable("policies");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.RuleCode).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Severity).HasMaxLength(10).IsRequired();
        builder.Property(p => p.ConfigJson).HasColumnType("text");
        builder.HasIndex(p => p.TenantId);
        builder.HasIndex(p => new { p.TenantId, p.RuleCode }).IsUnique();
        builder.Property(p => p.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(p => p.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasOne(p => p.Tenant)
            .WithMany()
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
