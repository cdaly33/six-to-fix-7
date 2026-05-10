namespace SixToFix.Application.Multitenancy;

/// <summary>
/// Scoped per-request tenant context. Populated by TenantContextMiddleware after JWT validation.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }
    string TenantSlug { get; }
    bool IsResolved { get; }
}
