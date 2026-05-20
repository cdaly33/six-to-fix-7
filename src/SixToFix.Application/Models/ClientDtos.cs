using System.ComponentModel.DataAnnotations;

namespace SixToFix.Application.Models;

public sealed class CreateClientDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [EmailAddress]
    [MaxLength(320)]
    public string? ContactEmail { get; set; }

    [MaxLength(2048)]
    public string? Notes { get; set; }
}

public sealed class UpdateClientDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [EmailAddress]
    [MaxLength(320)]
    public string? ContactEmail { get; set; }

    [MaxLength(2048)]
    public string? Notes { get; set; }
}

public sealed record ClientDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string? ContactEmail,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
