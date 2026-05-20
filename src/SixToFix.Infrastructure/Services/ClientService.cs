using SixToFix.Application.Models;
using SixToFix.Application.Services;
using SixToFix.Infrastructure.Data;

namespace SixToFix.Infrastructure.Services;

public sealed class ClientService : IClientService
{
    private readonly SixToFixDbContext _db;
    private readonly ILogger<ClientService> _logger;

    public ClientService(SixToFixDbContext db, ILogger<ClientService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Client>> GetAllForTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await ActiveClients(tenantId)
            .OrderBy(e => e.Name)
            .ToListAsync(ct);
    }

    public async Task<Client?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default)
    {
        return await ActiveClients(tenantId)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task<Guid> CreateAsync(CreateClientDto dto, Guid tenantId, CancellationToken ct = default)
    {
        var name = NormalizeName(dto.Name);
        await ThrowIfDuplicateNameAsync(tenantId, name, excludedId: null, ct);

        var now = DateTimeOffset.UtcNow;
        var client = new Client
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            ContactEmail = NormalizeOptional(dto.ContactEmail),
            Notes = NormalizeOptional(dto.Notes),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Clients.Add(client);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created client {ClientId} for tenant {TenantId}", client.Id, tenantId);
        return client.Id;
    }

    public async Task<Client?> UpdateAsync(Guid id, UpdateClientDto dto, Guid tenantId, CancellationToken ct = default)
    {
        var client = await ActiveClients(tenantId).FirstOrDefaultAsync(e => e.Id == id, ct);
        if (client is null) return null;

        var name = NormalizeName(dto.Name);
        await ThrowIfDuplicateNameAsync(tenantId, name, id, ct);

        client.Name = name;
        client.ContactEmail = NormalizeOptional(dto.ContactEmail);
        client.Notes = NormalizeOptional(dto.Notes);
        client.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return client;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid tenantId, CancellationToken ct = default)
    {
        var client = await ActiveClients(tenantId).FirstOrDefaultAsync(e => e.Id == id, ct);
        if (client is null) return false;

        client.IsActive = false;
        client.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Soft-deleted client {ClientId} for tenant {TenantId}", id, tenantId);
        return true;
    }

    private IQueryable<Client> ActiveClients(Guid tenantId) =>
        _db.Clients
            .IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId && e.IsActive);

    private async Task ThrowIfDuplicateNameAsync(Guid tenantId, string name, Guid? excludedId, CancellationToken ct)
    {
        var normalized = name.ToUpperInvariant();
        var duplicate = await ActiveClients(tenantId)
            .AnyAsync(e => e.Name.ToUpper() == normalized && (!excludedId.HasValue || e.Id != excludedId.Value), ct);

        if (duplicate)
        {
            throw new InvalidOperationException("A client with this name already exists for the tenant.");
        }
    }

    private static string NormalizeName(string value) => value.Trim();

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
