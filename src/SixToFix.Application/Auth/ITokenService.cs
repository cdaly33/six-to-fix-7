namespace SixToFix.Application.Auth;

public interface ITokenService
{
    string GenerateAccessToken(TokenRequest request);
}

public sealed record TokenRequest(
    Guid UserId,
    Guid TenantId,
    string TenantSlug,
    string Email,
    IReadOnlyList<string> Roles);
