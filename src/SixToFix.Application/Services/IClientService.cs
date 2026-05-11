using SixToFix.Application.Models;

namespace SixToFix.Application.Services;

public interface IClientService
{
    Task<IReadOnlyList<ClientDto>> GetClientsAsync(Guid tenantId, CancellationToken ct = default);
    Task<ClientDto?> GetClientByIdAsync(Guid clientId, Guid tenantId, CancellationToken ct = default);
    Task<ClientDto> CreateClientAsync(CreateClientRequest request, Guid tenantId, CancellationToken ct = default);
    Task<ClientDto?> UpdateClientAsync(Guid clientId, UpdateClientRequest request, Guid tenantId, CancellationToken ct = default);
    Task<bool> DeleteClientAsync(Guid clientId, Guid tenantId, CancellationToken ct = default);
}
