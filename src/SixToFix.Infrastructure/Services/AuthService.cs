using Microsoft.AspNetCore.Identity;
using SixToFix.Application.Auth;
using SixToFix.Infrastructure.Auth;

namespace SixToFix.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<LoginResult?> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive)
        {
            _logger.LogWarning("Login attempt for unknown or inactive email");
            return null;
        }

        var valid = await _userManager.CheckPasswordAsync(user, password);
        if (!valid)
        {
            _logger.LogWarning("Invalid password for user {UserId}", user.Id);
            return null;
        }

        return await BuildResultAsync(user);
    }

    public async Task<LoginResult?> ReissueTokenAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
            return null;

        return await BuildResultAsync(user);
    }

    private async Task<LoginResult> BuildResultAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var token = _tokenService.GenerateAccessToken(new TokenRequest(
            user.Id,
            user.TenantId,
            user.TenantSlug,
            user.Email!,
            [.. roles]));

        _logger.LogInformation("Token issued for user {UserId} in tenant {TenantId}", user.Id, user.TenantId);

        return new LoginResult(token, user.Email!, user.Id, user.TenantId, [.. roles]);
    }
}
