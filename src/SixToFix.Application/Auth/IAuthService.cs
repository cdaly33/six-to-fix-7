namespace SixToFix.Application.Auth;

public interface IAuthService
{
    /// <summary>
    /// Validates credentials and returns a login result containing a JWT, or null if invalid.
    /// </summary>
    Task<LoginResult?> LoginAsync(string email, string password, CancellationToken ct = default);

    /// <summary>
    /// Re-issues a fresh JWT for an already-authenticated user (no refresh token required).
    /// </summary>
    Task<LoginResult?> ReissueTokenAsync(Guid userId, CancellationToken ct = default);
}

public sealed record LoginResult(
    string AccessToken,
    string Email,
    Guid UserId,
    Guid TenantId,
    string TenantSlug,
    IReadOnlyList<string> Roles);
