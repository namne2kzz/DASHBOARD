using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Auth.Commands.Logout;

/// <summary>Handles <see cref="LogoutCommand"/>: revokes both the access token and its paired refresh token.</summary>
public sealed class LogoutCommandHandler(
    IApplicationDbContext db,
    IUnitOfWork uow)
    : IRequestHandler<LogoutCommand>
{
    /// <summary>Marks the <see cref="UserToken"/> record as fully revoked (access + refresh).</summary>
    /// <param name="command">The logout request containing the JWT ID and user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task Handle(LogoutCommand command, CancellationToken ct)
    {
        var token = await db.Set<UserToken>()
            .AsTracking()
            .FirstOrDefaultAsync(t => t.JwtId == command.JwtId && t.UserId == command.UserId, ct);

        if (token is null) return; // Token not found or already cleaned up — treat as success.

        var now = DateTime.UtcNow;

        token.IsRevoked             = true;
        token.RevokedAt             = now;
        token.RefreshTokenIsRevoked = true;
        token.RefreshTokenRevokedAt = now;

        await uow.CommitAsync(ct);
    }
}
