using DASHBOARD.Application.Auth.Commands.Login;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DASHBOARD.Application.Auth.Commands.SetPassword;

/// <summary>Handles <see cref="SetPasswordCommand"/>.</summary>
internal sealed class SetPasswordCommandHandler(
    IApplicationDbContext   db,
    IInvitationTokenService tokenService,
    IPasswordService        passwordService,
    ITokenService           jwtService,
    IOptions<Infrastructure.Identity.JwtSettings> jwtOptions)
    : IRequestHandler<SetPasswordCommand, Result<LoginResult>>
{
    private readonly Infrastructure.Identity.JwtSettings _jwt = jwtOptions.Value;

    /// <summary>
    /// Verifies the setup token, stores the hashed password, marks the token used, and issues an
    /// access + refresh token pair.
    /// </summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A signed-in <see cref="LoginResult"/>, or a failure when the token is unknown, expired or already used.</returns>
    public async Task<Result<LoginResult>> Handle(SetPasswordCommand request, CancellationToken ct)
    {
        var tokenHash = tokenService.Hash(request.RawToken);

        var setup = await db.Set<PasswordSetupToken>()
            .AsTracking()
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        // One message for unknown, used and expired tokens alike — a caller probing random tokens
        // learns nothing about which ones exist.
        if (setup is null || !setup.IsRedeemable(DateTime.UtcNow))
        {
            return Result<LoginResult>.Failure("This link is invalid, expired or has already been used.");
        }

        var user = setup.User;
        if (user is null || user.IsDeleted)
        {
            return Result<LoginResult>.Failure("This link is invalid, expired or has already been used.");
        }

        var org = await db.Set<Organization>().AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == user.OrgId, ct);

        if (org is null)
        {
            return Result<LoginResult>.Failure("This link is invalid, expired or has already been used.");
        }

        // ── Set the password ────────────────────────────────────────────────
        var (hash, salt) = passwordService.HashPassword(request.NewPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.Touch();

        // Single use. Marking it here, inside the same SaveChanges as the password, means a crash
        // cannot leave a redeemable token behind a password that was already set.
        setup.UsedAt = DateTime.UtcNow;
        setup.Touch();

        // ── Sign in ─────────────────────────────────────────────────────────
        var accessToken   = jwtService.GenerateToken(user.Id, user.Email, user.Name, user.OrgId);
        var refreshToken  = jwtService.GenerateRefreshToken();
        var refreshExpiry = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpiresInDays);

        db.Set<UserToken>().Add(new UserToken
        {
            UserId                = user.Id,
            JwtId                 = accessToken.JwtId,
            AccessTokenExpiresAt  = accessToken.ExpiresAt,
            IsRevoked             = false,
            RefreshTokenHash      = LoginCommandHandler.HashToken(refreshToken),
            RefreshTokenExpiresAt = refreshExpiry,
            RefreshTokenIsRevoked = false,
        });

        await db.SaveChangesAsync(ct);

        return Result<LoginResult>.Success(new LoginResult(
            AccessToken:           accessToken.Token,
            JwtId:                 accessToken.JwtId,
            AccessTokenExpiresAt:  accessToken.ExpiresAt,
            RefreshToken:          refreshToken,
            RefreshTokenExpiresAt: refreshExpiry,
            UserId:                user.Id,
            Name:                  user.Name,
            Email:                 user.Email,
            IsGlobalAdmin:         user.IsGlobalAdmin,
            OrgId:                 user.OrgId,
            OrgAlias:              org.Alias,
            OrgName:               org.Name,
            AvatarClass:           user.AvatarClass));
    }
}
