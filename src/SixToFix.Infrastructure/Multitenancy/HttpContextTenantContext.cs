namespace SixToFix.Infrastructure.Multitenancy;

/// <summary>
/// Scoped implementation. Set by TenantContextMiddleware after JWT auth succeeds.
/// </summary>
public sealed class HttpContextTenantContext : ITenantContext
{
    public Guid TenantId { get; private set; }
    public string TenantSlug { get; private set; } = string.Empty;
    public bool IsResolved { get; private set; }

    internal void Resolve(Guid tenantId, string tenantSlug)
    {
        TenantId = tenantId;
        TenantSlug = tenantSlug;
        IsResolved = true;
    }
}
