namespace SixToFix.Infrastructure.Models;

public sealed record CreateClientRequest(
    string Name,
    string? Industry = null,
    string? HubSpotCompanyId = null,
    string? Website = null);
