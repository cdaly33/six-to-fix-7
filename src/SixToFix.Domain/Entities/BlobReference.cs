namespace SixToFix.Domain.Entities;

public class BlobReference
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string ContainerName { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? LinkedEntityType { get; set; }
    public Guid? LinkedEntityId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
