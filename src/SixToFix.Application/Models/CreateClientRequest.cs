namespace SixToFix.Application.Models;

public sealed record CreateClientRequest(
    string Name,
    string? Industry = null,
    string? HubSpotCompanyId = null,
    string? Website = null);
