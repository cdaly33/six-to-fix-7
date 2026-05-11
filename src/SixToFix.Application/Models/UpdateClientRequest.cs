namespace SixToFix.Application.Models;

public sealed record UpdateClientRequest(
    string? Name = null,
    string? Industry = null,
    string? HubSpotCompanyId = null,
    string? Website = null,
    bool? IsActive = null);
