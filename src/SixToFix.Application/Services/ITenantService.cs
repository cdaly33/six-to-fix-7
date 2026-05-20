using SixToFix.Application.Models;
using SixToFix.Domain.Entities;

namespace SixToFix.Application.Services;

public interface ITenantService
{
    Task<TenantDto?> GetCurrentTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantDto?> UpdateTenantNameAsync(Guid tenantId, string newName, CancellationToken ct = default);
    Task<IReadOnlyList<TenantUserDto>> GetTenantUsersAsync(Guid tenantId, CancellationToken ct = default);
}
