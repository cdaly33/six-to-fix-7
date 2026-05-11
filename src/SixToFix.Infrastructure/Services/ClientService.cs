using System.Text.RegularExpressions;
using SixToFix.Application.Models;
using SixToFix.Application.Services;
using SixToFix.Infrastructure.Data;

namespace SixToFix.Infrastructure.Services;

public sealed partial class ClientService : IClientService
{
    private readonly SixToFixDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ClientService> _logger;

    public ClientService(
        SixToFixDbContext db,
        ITenantContext tenant,
        ILogger<ClientService> logger)
    {
        _db = db;
        _tenant = tenant;
        _logger = logger;
    }

    // Global query filter on SixToFixDbContext already scopes Clients to the current tenant.
    // The tenantId parameter is part of the contract for call-site clarity and documentation.

    public async Task<IReadOnlyList<ClientDto>> GetClientsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var clients = await _db.Clients
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        _logger.LogDebug("Retrieved {Count} clients for tenant {TenantId}", clients.Count, tenantId);

        return clients.Select(MapToDto).ToList();
    }

    public async Task<ClientDto?> GetClientByIdAsync(Guid clientId, Guid tenantId, CancellationToken ct = default)
    {
        var client = await _db.Clients
            .FirstOrDefaultAsync(c => c.Id == clientId, ct);

        if (client is null)
        {
            _logger.LogWarning("Client {ClientId} not found for tenant {TenantId}", clientId, tenantId);
            return null;
        }

        return MapToDto(client);
    }

    public async Task<ClientDto> CreateClientAsync(CreateClientRequest request, Guid tenantId, CancellationToken ct = default)
    {
        var slug = GenerateSlug(request.Name);

        var client = new Client
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            Name = request.Name,
            Slug = slug,
            Industry = request.Industry,
            HubSpotCompanyId = request.HubSpotCompanyId,
            Website = request.Website,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Clients.Add(client);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created client {ClientId} with slug {Slug} for tenant {TenantId}",
            client.Id, slug, tenantId);

        return MapToDto(client);
    }

    public async Task<ClientDto?> UpdateClientAsync(
        Guid clientId,
        UpdateClientRequest request,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var client = await _db.Clients
            .FirstOrDefaultAsync(c => c.Id == clientId, ct);

        if (client is null)
        {
            _logger.LogWarning("Client {ClientId} not found for update, tenant {TenantId}", clientId, tenantId);
            return null;
        }

        if (request.Name is not null)
        {
            client.Name = request.Name;
            client.Slug = GenerateSlug(request.Name);
        }

        if (request.Industry is not null)
            client.Industry = request.Industry;

        if (request.HubSpotCompanyId is not null)
            client.HubSpotCompanyId = request.HubSpotCompanyId;

        if (request.Website is not null)
            client.Website = request.Website;

        if (request.IsActive is not null)
            client.IsActive = request.IsActive.Value;

        client.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Updated client {ClientId} for tenant {TenantId}", clientId, tenantId);

        return MapToDto(client);
    }

    public async Task<bool> DeleteClientAsync(Guid clientId, Guid tenantId, CancellationToken ct = default)
    {
        var client = await _db.Clients
            .FirstOrDefaultAsync(c => c.Id == clientId, ct);

        if (client is null)
        {
            _logger.LogWarning("Client {ClientId} not found for delete, tenant {TenantId}", clientId, tenantId);
            return false;
        }

        // Soft-delete: sf_app role has no DELETE privilege (ADR-007).
        client.IsActive = false;
        client.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Soft-deleted client {ClientId} for tenant {TenantId}", clientId, tenantId);

        return true;
    }

    private static ClientDto MapToDto(Client c) =>
        new(c.Id, c.TenantId, c.Name, c.Slug, c.Industry, c.HubSpotCompanyId, c.Website, c.IsActive, c.CreatedAt, c.UpdatedAt);

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[^a-z0-9\-]")]
    private static partial Regex NonSlugCharRegex();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex MultiDashRegex();

    private static string GenerateSlug(string name)
    {
        var slug = name.Trim().ToLowerInvariant();
        slug = WhitespaceRegex().Replace(slug, "-");
        slug = NonSlugCharRegex().Replace(slug, string.Empty);
        slug = MultiDashRegex().Replace(slug, "-");
        return slug.Trim('-');
    }
}
