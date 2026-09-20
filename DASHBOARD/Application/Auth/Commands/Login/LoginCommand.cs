using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Auth.Commands.Login;

/// <summary>Authenticates a user within an organization with email and password, returning a signed JWT + refresh token on success.</summary>
/// <param name="OrgAlias">The organization (tenant) alias the user belongs to.</param>
/// <param name="Email">The user's registered email address.</param>
/// <param name="Password">The plaintext password to verify.</param>
public record LoginCommand(string OrgAlias, string Email, string Password) : IRequest<Result<LoginResult>>;

/// <summary>Result returned by a successful <see cref="LoginCommand"/> or <c>RefreshTokenCommand</c>.</summary>
/// <param name="AccessToken">The signed JWT — include as <c>Authorization: Bearer {AccessToken}</c> on subsequent requests.</param>
/// <param name="JwtId">The <c>jti</c> claim embedded in the access token, used for server-side revocation.</param>
/// <param name="AccessTokenExpiresAt">UTC timestamp when the access token expires.</param>
/// <param name="RefreshToken">Opaque token used to obtain a new access token when it expires. Store securely (HttpOnly cookie recommended).</param>
/// <param name="RefreshTokenExpiresAt">UTC timestamp when the refresh token expires.</param>
/// <param name="UserId">The authenticated user's unique identifier.</param>
/// <param name="Name">The authenticated user's display name.</param>
/// <param name="Email">The authenticated user's email address.</param>
/// <param name="IsGlobalAdmin">Whether the authenticated user has system-wide admin privileges.</param>
/// <param name="OrgId">The organization (tenant) the user belongs to.</param>
/// <param name="OrgAlias">The organization's URL alias.</param>
/// <param name="AvatarClass">Tailwind CSS background class for the user's avatar chip.</param>
public record LoginResult(
    string AccessToken,
    string JwtId,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    Guid UserId,
    string Name,
    string Email,
    bool IsGlobalAdmin,
    Guid OrgId,
    string OrgAlias,
    string AvatarClass);
