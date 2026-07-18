using DASHBOARD.Application.Auth.Commands.Login;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DASHBOARD.Application.Auth.Commands.GoogleLogin;

/// <summary>Handles <see cref="GoogleLoginCommand"/>.</summary>
internal sealed class GoogleLoginCommandHandler(
    IApplicationDbContext db,
    IGoogleAuthService googleAuth,
    ITokenService tokenService,
    IUnitOfWork uow,
    IOptions<Infrastructure.Identity.JwtSettings> jwtOptions) : IRequestHandler<GoogleLoginCommand, Result<LoginResult>>
{
    private readonly Infrastructure.Identity.JwtSettings _jwt = jwtOptions.Value;

    /// <summary>
    /// Verifies the Google identity and signs in the account already linked to that Google subject —
    /// there is no invitation to consume here, so an unmatched Google account is simply rejected.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A signed-in <see cref="LoginResult"/> on success, or a descriptive failure message.</returns>
    public async Task<Result<LoginResult>> Handle(GoogleLoginCommand command, CancellationToken ct)
    {
        GoogleUserInfo googleUser;
        try
        {
            googleUser = await googleAuth.ValidateAsync(command.GoogleIdToken, ct);
        }
        catch
        {
            return Result<LoginResult>.Failure("Google token verification failed. Please sign in with Google again.");
        }

        var user = await db.Set<User>()
            .AsTracking()
            .FirstOrDefaultAsync(u => u.GoogleSubjectId == googleUser.Subject, ct);

        if (user is null)
            return Result<LoginResult>.Failure(
                "No account is linked to this Google account. Ask a repository admin to invite you first.");

        var accessToken   = tokenService.GenerateToken(user.Id, user.Email, user.Name);
        var refreshToken  = tokenService.GenerateRefreshToken();
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

        await uow.CommitAsync(ct);

        return Result<LoginResult>.Success(new LoginResult(
            AccessToken:           accessToken.Token,
            JwtId:                 accessToken.JwtId,
            AccessTokenExpiresAt:  accessToken.ExpiresAt,
            RefreshToken:          refreshToken,
            RefreshTokenExpiresAt: refreshExpiry,
            UserId:                user.Id,
            Name:                  user.Name,
            Email:                 user.Email,
            IsGlobalAdmin:         user.IsGlobalAdmin));
    }
}
