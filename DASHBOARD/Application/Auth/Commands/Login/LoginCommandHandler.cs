using System.Security.Cryptography;
using System.Text;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DASHBOARD.Application.Auth.Commands.Login;

/// <summary>Handles <see cref="LoginCommand"/>: verifies credentials with PBKDF2 and issues an access + refresh token pair.</summary>
public sealed class LoginCommandHandler(
    IApplicationDbContext db,
    IPasswordService passwordService,
    ITokenService tokenService,
    IUnitOfWork uow,
    IOptions<Infrastructure.Identity.JwtSettings> jwtOptions)
    : IRequestHandler<LoginCommand, Result<LoginResult>>
{
    private readonly Infrastructure.Identity.JwtSettings _jwt = jwtOptions.Value;

    /// <summary>Validates credentials, generates access + refresh tokens, persists the token record.</summary>
    /// <param name="command">Login credentials.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result{T}.Success"/> with a <see cref="LoginResult"/>, or <see cref="Result{T}.Failure"/> on invalid email or password.</returns>
    public async Task<Result<LoginResult>> Handle(LoginCommand command, CancellationToken ct)
    {
        var alias = command.OrgAlias.Trim().ToLowerInvariant();
        var org = await db.Set<Organization>().AsNoTracking()
            .FirstOrDefaultAsync(o => o.Alias == alias, ct);

        // Same error for unknown org, unknown email, or wrong password — prevents enumeration.
        if (org is null)
            return Result<LoginResult>.Failure("Invalid organization, email or password.");

        var user = await db.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.OrgId == org.Id && u.Email == command.Email.ToLowerInvariant(), ct);

        if (user is null || !passwordService.VerifyPassword(command.Password, user.PasswordHash, user.PasswordSalt))
            return Result<LoginResult>.Failure("Invalid organization, email or password.");

        var accessToken   = tokenService.GenerateToken(user.Id, user.Email, user.Name, user.OrgId);
        var refreshToken  = tokenService.GenerateRefreshToken();
        var refreshExpiry = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpiresInDays);

        var userToken = new UserToken
        {
            UserId                 = user.Id,
            JwtId                  = accessToken.JwtId,
            AccessTokenExpiresAt   = accessToken.ExpiresAt,
            IsRevoked              = false,
            RefreshTokenHash       = HashToken(refreshToken),
            RefreshTokenExpiresAt  = refreshExpiry,
            RefreshTokenIsRevoked  = false,
        };

        db.Set<UserToken>().Add(userToken);
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
            IsGlobalAdmin:         user.IsGlobalAdmin,
            OrgId:                 org.Id,
            OrgAlias:              org.Alias,
            AvatarClass:           user.AvatarClass));
    }

    /// <summary>Computes SHA-256 hash of the refresh token for safe database storage.</summary>
    /// <param name="token">The plaintext refresh token.</param>
    /// <returns>Base64-encoded SHA-256 hash.</returns>
    internal static string HashToken(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
