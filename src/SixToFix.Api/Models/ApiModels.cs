using System.Text.Json.Serialization;

namespace SixToFix.Api.Models;

// ── Auth ──────────────────────────────────────────────────────────────────────

public sealed record LoginRequest(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("password")] string Password);

public sealed record LoginResponse(
    [property: JsonPropertyName("accessToken")] string AccessToken,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("userId")] Guid UserId,
    [property: JsonPropertyName("tenantId")] Guid TenantId,
    [property: JsonPropertyName("roles")] IReadOnlyList<string> Roles);

// ── Audit Runs ────────────────────────────────────────────────────────────────

public sealed record CreateAuditRunRequest(
    [property: JsonPropertyName("clientId")] Guid ClientId);

// ── Reviewer Workflow ──────────────────────────────────────────────────────────

public sealed record RejectRequest(
    [property: JsonPropertyName("reason")] string? Reason);

// ── HubSpot Webhook ───────────────────────────────────────────────────────────

public sealed record HubSpotWebhookPayload(
    [property: JsonPropertyName("auditRunId")] Guid AuditRunId,
    [property: JsonPropertyName("clientSlug")] string ClientSlug,
    [property: JsonPropertyName("tier")] string Tier,
    [property: JsonPropertyName("compositeScore")] decimal CompositeScore,
    [property: JsonPropertyName("publishedAt")] DateTimeOffset? PublishedAt);
