using DASHBOARD.Application.Auth.Commands.Login;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DASHBOARD.Application.Invitations.Commands.AcceptInvitation;

/// <summary>Handles <see cref="AcceptInvitationCommand"/>.</summary>
internal sealed class AcceptInvitationCommandHandler(
    IApplicationDbContext db,
    IInvitationTokenService tokenService,
    IGoogleAuthService googleAuth,
    ITokenService jwtService,
    IOptions<Infrastructure.Identity.JwtSettings> jwtOptions,
    IAppSettings settings) : IRequestHandler<AcceptInvitationCommand, Result<LoginResult>>
{
    private readonly Infrastructure.Identity.JwtSettings _jwt = jwtOptions.Value;

    /// <summary>
    /// Verifies the invite token and Google identity, creates the user account and repository membership,
    /// and issues an access + refresh token pair (invited users are Google-only and have no password to log in with).
    /// If the Google account was previously invited to another repo, only a new membership is created.
    /// </summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A signed-in <see cref="LoginResult"/> on success, or a descriptive failure message.</returns>
    public async Task<Result<LoginResult>> Handle(AcceptInvitationCommand request, CancellationToken ct)
    {
        var tokenHash = tokenService.Hash(request.RawToken);

        var invitation = await db.Set<Invitation>()
            .AsTracking()
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash && i.Status == InvitationStatus.Pending, ct);

        if (invitation is null)
            return Result<LoginResult>.Failure("Invitation not found or already used.");

        if (invitation.ExpiresAt < DateTime.UtcNow)
        {
            invitation.Status = InvitationStatus.Expired;
            await db.SaveChangesAsync(ct);
            return Result<LoginResult>.Failure("Invitation has expired. Please ask an admin to send a new invite.");
        }

        GoogleUserInfo googleUser;
        try
        {
            googleUser = await googleAuth.ValidateAsync(request.GoogleIdToken, ct);
        }
        catch
        {
            return Result<LoginResult>.Failure("Google token verification failed. Please sign in with Google again.");
        }

        // Enforce email match — the Google account must belong to the invited address.
        if (!string.Equals(googleUser.Email, invitation.Email, StringComparison.OrdinalIgnoreCase))
            return Result<LoginResult>.Failure(
                "The Google account email does not match the invited email address.");

        // Resolve or create the User record.
        var existingUser = await db.Set<User>()
            .AsTracking()
            .FirstOrDefaultAsync(u => u.GoogleSubjectId == googleUser.Subject, ct);

        Guid   userId;
        string userName;
        string userEmail;
        bool   isGlobalAdmin;
        if (existingUser is not null)
        {
            userId        = existingUser.Id;
            userName      = existingUser.Name;
            userEmail     = existingUser.Email;
            isGlobalAdmin = existingUser.IsGlobalAdmin;
        }
        else
        {
            var newUser = new User
            {
                Name            = googleUser.Name ?? googleUser.Email,
                Email           = googleUser.Email,
                AuthProvider    = AuthProvider.Google,
                GoogleSubjectId = googleUser.Subject,
                // Google users have no password — empty strings satisfy the non-null column.
                PasswordHash    = string.Empty,
                PasswordSalt    = string.Empty,
                AvatarClass     = settings.DefaultGoogleUserAvatarClass,
                // Apply the manager chosen by the inviter (org hierarchy), if any.
                ManagerId       = invitation.ManagerId,
            };
            db.Set<User>().Add(newUser);
            userId        = newUser.Id;
            userName      = newUser.Name;
            userEmail     = newUser.Email;
            isGlobalAdmin = newUser.IsGlobalAdmin;
        }

        // Add to repository if not already a member.
        var alreadyMember = await db.Set<RepositoryMember>()
            .AsNoTracking()
            .AnyAsync(m => m.UserId == userId && m.RepositoryId == invitation.RepositoryId, ct);

        if (!alreadyMember)
        {
            // Use the discipline + role chosen by the inviter at invite time (validated to still
            // exist for this repo — a role could have been deleted since the invite was sent).
            var roleStillExists = await db.Set<Role>().AsNoTracking()
                .AnyAsync(r => r.Id == invitation.RoleId && (r.IsDefault || r.RepositoryId == invitation.RepositoryId), ct);
            if (!roleStillExists)
                return Result<LoginResult>.Failure(
                    "The role assigned to this invitation no longer exists. Please ask an admin to send a new invite.");

            db.Set<RepositoryMember>().Add(new RepositoryMember
            {
                UserId       = userId,
                RepositoryId = invitation.RepositoryId,
                DefaultRole  = invitation.DefaultRole,
                RoleId       = invitation.RoleId,
            });
        }

        invitation.Status     = InvitationStatus.Accepted;
        invitation.AcceptedAt = DateTime.UtcNow;

        // Invited users are Google-only (no password), so they can't use the regular login
        // endpoint afterwards — issue a session here exactly like LoginCommandHandler does.
        var accessToken   = jwtService.GenerateToken(userId, userEmail, userName);
        var refreshToken  = jwtService.GenerateRefreshToken();
        var refreshExpiry = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpiresInDays);

        db.Set<UserToken>().Add(new UserToken
        {
            UserId                = userId,
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
            UserId:                userId,
            Name:                  userName,
            Email:                 userEmail,
            IsGlobalAdmin:         isGlobalAdmin));
    }
}
