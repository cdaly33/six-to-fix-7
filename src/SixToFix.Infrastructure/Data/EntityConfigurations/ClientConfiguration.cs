using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SixToFix.Infrastructure.Data.EntityConfigurations;

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("clients");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.ContactEmail)
            .HasMaxLength(320);

        builder.Property(e => e.Notes)
            .HasMaxLength(2048);

        builder.Property(e => e.IsActive)
            .HasDefaultValue(true);

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("now()");

        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("now()");

        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => new { e.TenantId, e.Name })
            .IsUnique()
            .HasFilter("is_active = true");
    }
}
