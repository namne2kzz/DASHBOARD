namespace DASHBOARD.Application.Common.Interfaces;

/// <summary>Result returned by <see cref="ITokenService.GenerateToken"/>.</summary>
/// <param name="Token">The compact-serialized, signed JWT string.</param>
/// <param name="JwtId">The <c>jti</c> claim embedded in the token — used for server-side revocation.</param>
/// <param name="ExpiresAt">UTC timestamp when the token expires.</param>
public record TokenResult(string Token, string JwtId, DateTime ExpiresAt);

/// <summary>Contract for generating authentication tokens. Implementation lives in Infrastructure.</summary>
public interface ITokenService
{
    /// <summary>Generates a signed JWT access token for the given user identity.</summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="email">The user's email address embedded as a claim.</param>
    /// <param name="name">The user's display name embedded as a claim.</param>
    /// <returns>A <see cref="TokenResult"/> containing the signed JWT, its ID, and expiry.</returns>
    TokenResult GenerateToken(Guid userId, string email, string name);

    /// <summary>
    /// Generates a cryptographically secure opaque refresh token (512-bit, Base64-encoded).
    /// The caller is responsible for hashing it before storage — never store the plaintext.
    /// </summary>
    /// <returns>A Base64-encoded random refresh token string.</returns>
    string GenerateRefreshToken();
}
