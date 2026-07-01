using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Users.Commands.UpdateProfile;

/// <summary>Handles <see cref="UpdateProfileCommand"/>: enforces self-or-admin rule then persists the name and avatar changes.</summary>
public sealed class UpdateProfileCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<UpdateProfileCommand, Result>
{
    /// <summary>Validates requester identity, applies the profile change, and commits.</summary>
    /// <param name="command">The update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success; <see cref="Result.Failure"/> on business-rule violation.</returns>
    public async Task<Result> Handle(UpdateProfileCommand command, CancellationToken ct)
    {
        // Only the user themselves or a global admin may update a profile.
        if (!user.IsSelf(command.TargetUserId) && !await user.IsGlobalAdminAsync(ct))
            return Result.Failure("You do not have permission to update this profile.");

        var target = await db.Set<User>()
            .AsTracking()
            .FirstOrDefaultAsync(u => u.Id == command.TargetUserId, ct)
            ?? throw new NotFoundException(nameof(User), command.TargetUserId);

        target.Name        = command.Name;
        target.AvatarClass = command.AvatarClass;
        target.Touch();

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
