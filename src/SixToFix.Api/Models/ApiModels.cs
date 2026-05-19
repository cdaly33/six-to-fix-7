using System.Text.Json.Serialization;

namespace SixToFix.Api.Models;

public sealed record LoginRequest([property: JsonPropertyName("email")] string Email, [property: JsonPropertyName("password")] string Password);
public sealed record LoginResponse([property: JsonPropertyName("accessToken")] string AccessToken, [property: JsonPropertyName("email")] string Email, [property: JsonPropertyName("userId")] Guid UserId, [property: JsonPropertyName("tenantId")] Guid TenantId, [property: JsonPropertyName("roles")] IReadOnlyList<string> Roles);
