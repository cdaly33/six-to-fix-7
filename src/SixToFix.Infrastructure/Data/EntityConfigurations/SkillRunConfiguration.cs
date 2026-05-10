using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SixToFix.Domain.Entities;

namespace SixToFix.Infrastructure.Data.EntityConfigurations;

public sealed class SkillRunConfiguration : IEntityTypeConfiguration<SkillRun>
{
    public void Configure(EntityTypeBuilder<SkillRun> builder)
    {
        builder.ToTable("skill_runs");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.SkillName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Category).HasMaxLength(30).IsRequired();
        builder.Property(s => s.Status).HasMaxLength(20).IsRequired();
        builder.Property(s => s.InputBlobReference).HasMaxLength(500);
        builder.Property(s => s.OutputBlobReference).HasMaxLength(500);
        builder.Property(s => s.ConfidenceScore).HasPrecision(4, 3);
        builder.Property(s => s.FailureReason).HasColumnType("text");
        builder.HasIndex(s => s.TenantId);
        builder.HasIndex(s => s.AuditRunId);
        builder.Property(s => s.CreatedAt).HasDefaultValueSql("now()");

        builder.HasOne(s => s.Tenant)
            .WithMany()
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.AuditRun)
            .WithMany(r => r.SkillRuns)
            .HasForeignKey(s => s.AuditRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
