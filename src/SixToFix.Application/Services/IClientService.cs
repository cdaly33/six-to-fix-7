using SixToFix.Application.Models;
using SixToFix.Domain.Entities;

namespace SixToFix.Application.Services;

public interface IClientService
{
    Task<IReadOnlyList<Client>> GetAllForTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<Client?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateClientDto dto, Guid tenantId, CancellationToken ct = default);
    Task<Client?> UpdateAsync(Guid id, UpdateClientDto dto, Guid tenantId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid tenantId, CancellationToken ct = default);
}
