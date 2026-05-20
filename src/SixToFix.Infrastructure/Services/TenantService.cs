using Microsoft.AspNetCore.Identity;
using SixToFix.Application.Models;
using SixToFix.Application.Services;
using SixToFix.Domain.Entities;
using SixToFix.Infrastructure.Auth;
using SixToFix.Infrastructure.Data;

namespace SixToFix.Infrastructure.Services;

public sealed class TenantService : ITenantService
{
    private readonly SixToFixDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<TenantService> _logger;

    public TenantService(SixToFixDbContext db, UserManager<ApplicationUser> userManager, ILogger<TenantService> logger)
    {
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<TenantDto?> GetCurrentTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants
            .Where(t => t.Id == tenantId && t.IsActive)
            .Select(t => new TenantDto(t.Id, t.Name, t.Slug, t.CreatedAt))
            .FirstOrDefaultAsync(ct);

        return tenant;
    }

    public async Task<TenantDto?> UpdateTenantNameAsync(Guid tenantId, string newName, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive, ct);
        if (tenant is null)
        {
            return null;
        }

        var normalizedName = newName.Trim();
        if (string.IsNullOrEmpty(normalizedName))
        {
            throw new InvalidOperationException("Tenant name cannot be empty.");
        }

        tenant.Name = normalizedName;
        tenant.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Updated tenant {TenantId} name to {TenantName}", tenantId, normalizedName);

        return new TenantDto(tenant.Id, tenant.Name, tenant.Slug, tenant.CreatedAt);
    }

    public async Task<IReadOnlyList<TenantUserDto>> GetTenantUsersAsync(Guid tenantId, CancellationToken ct = default)
    {
        var users = await _db.Users
            .Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.Email)
            .ToListAsync(ct);

        var userDtos = new List<TenantUserDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "Viewer";

            userDtos.Add(new TenantUserDto(
                user.Id,
                user.Email ?? string.Empty,
                user.FullName,
                role,
                user.IsActive,
                null,
                user.CreatedAt
            ));
        }

        return userDtos;
    }
}
