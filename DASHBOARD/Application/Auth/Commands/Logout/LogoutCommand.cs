using MediatR;

namespace DASHBOARD.Application.Auth.Commands.Logout;

/// <summary>Revokes the current user's JWT server-side, preventing further use of the same token.</summary>
/// <param name="JwtId">The <c>jti</c> claim of the token to revoke.</param>
/// <param name="UserId">The ID of the user performing the logout.</param>
public record LogoutCommand(string JwtId, Guid UserId) : IRequest;
