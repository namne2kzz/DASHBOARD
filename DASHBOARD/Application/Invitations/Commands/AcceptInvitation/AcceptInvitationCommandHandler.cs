using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Constants;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Invitations.Commands.AcceptInvitation;

/// <summary>Handles <see cref="AcceptInvitationCommand"/>.</summary>
internal sealed class AcceptInvitationCommandHandler(
    IApplicationDbContext db,
    IInvitationTokenService tokenService,
    IGoogleAuthService googleAuth,
    IAppSettings settings) : IRequestHandler<AcceptInvitationCommand, Result<Guid>>
{
    /// <summary>
    /// Verifies the invite token and Google identity, then creates the user account and repository membership.
    /// If the Google account was previously invited to another repo, only a new membership is created.
    /// </summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The user's ID on success, or a descriptive failure message.</returns>
    public async Task<Result<Guid>> Handle(AcceptInvitationCommand request, CancellationToken ct)
    {
        var tokenHash = tokenService.Hash(request.RawToken);

        var invitation = await db.Set<Invitation>()
            .AsTracking()
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash && i.Status == InvitationStatus.Pending, ct);

        if (invitation is null)
            return Result<Guid>.Failure("Invitation not found or already used.");

        if (invitation.ExpiresAt < DateTime.UtcNow)
        {
            invitation.Status = InvitationStatus.Expired;
            await db.SaveChangesAsync(ct);
            return Result<Guid>.Failure("Invitation has expired. Please ask an admin to send a new invite.");
        }

        GoogleUserInfo googleUser;
        try
        {
            googleUser = await googleAuth.ValidateAsync(request.GoogleIdToken, ct);
        }
        catch
        {
            return Result<Guid>.Failure("Google token verification failed. Please sign in with Google again.");
        }

        // Enforce email match — the Google account must belong to the invited address.
        if (!string.Equals(googleUser.Email, invitation.Email, StringComparison.OrdinalIgnoreCase))
            return Result<Guid>.Failure(
                "The Google account email does not match the invited email address.");

        // Resolve or create the User record.
        var existingUser = await db.Set<User>()
            .AsTracking()
            .FirstOrDefaultAsync(u => u.GoogleSubjectId == googleUser.Subject, ct);

        Guid userId;
        if (existingUser is not null)
        {
            userId = existingUser.Id;
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
            };
            db.Set<User>().Add(newUser);
            userId = newUser.Id;
        }

        // Add to repository if not already a member.
        var alreadyMember = await db.Set<RepositoryMember>()
            .AsNoTracking()
            .AnyAsync(m => m.UserId == userId && m.RepositoryId == invitation.RepositoryId, ct);

        if (!alreadyMember)
        {
            // Assign the repository's own Developer default role (resolved by name, not a shared id).
            var developerRole = await db.Set<Role>().AsNoTracking()
                .FirstOrDefaultAsync(r => r.IsDefault
                                       && r.RepositoryId == invitation.RepositoryId
                                       && r.Name == DefaultRoleDefinitions.Developer, ct)
                ?? throw new InvalidOperationException("The repository has no Developer role configured.");

            db.Set<RepositoryMember>().Add(new RepositoryMember
            {
                UserId       = userId,
                RepositoryId = invitation.RepositoryId,
                DefaultRole  = DefaultRoleDefinitions.Developer,
                RoleId       = developerRole.Id,
            });
        }

        invitation.Status     = InvitationStatus.Accepted;
        invitation.AcceptedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result<Guid>.Success(userId);
    }
}
