using DASHBOARD.Application.Auth.Commands.Login;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DASHBOARD.Application.Auth.Commands.RefreshToken;

/// <summary>
/// Handles <see cref="RefreshTokenCommand"/>: validates the refresh token, rotates both tokens,
/// and returns a new access + refresh token pair.
/// </summary>
public sealed class RefreshTokenCommandHandler(
    IApplicationDbContext db,
    ITokenService tokenService,
    IUnitOfWork uow,
    IOptions<Infrastructure.Identity.JwtSettings> jwtOptions)
    : IRequestHandler<RefreshTokenCommand, LoginResult>
{
    private readonly Infrastructure.Identity.JwtSettings _jwt = jwtOptions.Value;

    /// <summary>
    /// Validates the refresh token, revokes the old record, issues a new token pair (rotation).
    /// </summary>
    /// <param name="command">The refresh request containing the plaintext refresh token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A new <see cref="LoginResult"/> with fresh access and refresh tokens.</returns>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when the refresh token is not found, already revoked, or expired.
    /// </exception>
    public async Task<LoginResult> Handle(RefreshTokenCommand command, CancellationToken ct)
    {
        var tokenHash = LoginCommandHandler.HashToken(command.RefreshToken);

        var existingToken = await db.Set<UserToken>()
            .Include(t => t.User)
            .FirstOrDefaultAsync(
                t => t.RefreshTokenHash == tokenHash
                  && !t.RefreshTokenIsRevoked
                  && t.RefreshTokenExpiresAt > DateTime.UtcNow,
                ct);

        // Same error for all failure cases — prevents token-enumeration attacks.
        if (existingToken?.User is null)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        // ── Token rotation: revoke old record ────────────────────────────────
        existingToken.IsRevoked              = true;
        existingToken.RevokedAt              = DateTime.UtcNow;
        existingToken.RefreshTokenIsRevoked  = true;
        existingToken.RefreshTokenRevokedAt  = DateTime.UtcNow;

        // ── Issue new token pair ──────────────────────────────────────────────
        var user          = existingToken.User;
        var newAccess     = tokenService.GenerateToken(user.Id, user.Email, user.Name);
        var newRefresh    = tokenService.GenerateRefreshToken();
        var refreshExpiry = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpiresInDays);

        var newToken = new UserToken
        {
            UserId                 = user.Id,
            JwtId                  = newAccess.JwtId,
            AccessTokenExpiresAt   = newAccess.ExpiresAt,
            IsRevoked              = false,
            RefreshTokenHash       = LoginCommandHandler.HashToken(newRefresh),
            RefreshTokenExpiresAt  = refreshExpiry,
            RefreshTokenIsRevoked  = false,
        };

        db.Set<UserToken>().Add(newToken);
        await uow.CommitAsync(ct);

        return new LoginResult(
            AccessToken:           newAccess.Token,
            JwtId:                 newAccess.JwtId,
            AccessTokenExpiresAt:  newAccess.ExpiresAt,
            RefreshToken:          newRefresh,
            RefreshTokenExpiresAt: refreshExpiry,
            UserId:                user.Id,
            Name:                  user.Name,
            Email:                 user.Email,
            IsGlobalAdmin:         user.IsGlobalAdmin);
    }
}
