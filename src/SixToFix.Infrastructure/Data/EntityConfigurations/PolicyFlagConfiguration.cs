using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SixToFix.Domain.Entities;

namespace SixToFix.Infrastructure.Data.EntityConfigurations;

public sealed class PolicyFlagConfiguration : IEntityTypeConfiguration<PolicyFlag>
{
    public void Configure(EntityTypeBuilder<PolicyFlag> builder)
    {
        builder.ToTable("policy_flags");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.RuleCode).HasMaxLength(50).IsRequired();
        builder.Property(f => f.Severity).HasMaxLength(10).IsRequired();
        builder.Property(f => f.Detail).HasColumnType("text");
        builder.HasIndex(f => f.TenantId);
        builder.HasIndex(f => f.SkillRunId);
        builder.Property(f => f.CreatedAt).HasDefaultValueSql("now()");

        builder.HasOne(f => f.Tenant)
            .WithMany()
            .HasForeignKey(f => f.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.SkillRun)
            .WithMany(s => s.PolicyFlags)
            .HasForeignKey(f => f.SkillRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
