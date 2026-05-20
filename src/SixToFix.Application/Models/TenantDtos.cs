using System.ComponentModel.DataAnnotations;

namespace SixToFix.Application.Models;

public sealed record TenantDto(
    Guid Id,
    string Name,
    string Slug,
    DateTimeOffset CreatedAt);

public sealed record TenantUserDto(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    bool IsActive,
    DateTimeOffset? LastLogin,
    DateTimeOffset CreatedAt);

public sealed class UpdateTenantNameDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
}
