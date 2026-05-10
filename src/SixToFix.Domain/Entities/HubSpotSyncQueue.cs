namespace SixToFix.Domain.Entities;

public class HubSpotSyncQueue
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? ClientId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public int RetryCount { get; set; }
    public string? LastErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Client? Client { get; set; }
}
