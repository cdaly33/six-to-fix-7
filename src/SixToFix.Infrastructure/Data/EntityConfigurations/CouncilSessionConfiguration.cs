using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SixToFix.Domain.Entities;

namespace SixToFix.Infrastructure.Data.EntityConfigurations;

public sealed class CouncilSessionConfiguration : IEntityTypeConfiguration<CouncilSession>
{
    public void Configure(EntityTypeBuilder<CouncilSession> builder)
    {
        builder.ToTable("council_sessions");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Status).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Decision).HasMaxLength(20).IsRequired();
        builder.Property(c => c.AdvocateOutputJson).HasColumnType("text");
        builder.Property(c => c.SkepticOutputJson).HasColumnType("text");
        builder.Property(c => c.JudgeOutputJson).HasColumnType("text");
        builder.Property(c => c.Rationale).HasColumnType("text");
        builder.Property(c => c.AdjustedScore).HasPrecision(5, 2);
        builder.HasIndex(c => c.TenantId);
        builder.HasIndex(c => c.SkillRunId);
        builder.Property(c => c.CreatedAt).HasDefaultValueSql("now()");

        builder.HasOne(c => c.Tenant)
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.SkillRun)
            .WithMany()
            .HasForeignKey(c => c.SkillRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
