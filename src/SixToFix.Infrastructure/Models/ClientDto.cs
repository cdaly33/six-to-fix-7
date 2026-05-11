namespace SixToFix.Infrastructure.Models;

public sealed record ClientDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string Slug,
    string? Industry,
    string? HubSpotCompanyId,
    string? Website,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
