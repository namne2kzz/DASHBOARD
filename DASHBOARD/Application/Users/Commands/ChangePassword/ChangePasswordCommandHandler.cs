using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Users.Commands.ChangePassword;

/// <summary>Handles <see cref="ChangePasswordCommand"/>: verifies old password then replaces the hash + salt pair.</summary>
public sealed class ChangePasswordCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IPasswordService      passwordService,
    IUnitOfWork           uow) : IRequestHandler<ChangePasswordCommand, Result>
{
    /// <summary>Verifies the old password, hashes the new one, and commits the change.</summary>
    /// <param name="command">The change-password request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success; <see cref="Result.Failure"/> when the old password is wrong or requester is not the account owner.</returns>
    public async Task<Result> Handle(ChangePasswordCommand command, CancellationToken ct)
    {
        // Only the owner can change their own password.
        if (!user.IsSelf(command.TargetUserId))
            return Result.Failure("You may only change your own password.");

        var target = await db.Set<User>()
            .AsTracking()
            .FirstOrDefaultAsync(u => u.Id == command.TargetUserId, ct)
            ?? throw new NotFoundException(nameof(User), command.TargetUserId);

        if (!passwordService.VerifyPassword(command.OldPassword, target.PasswordHash, target.PasswordSalt))
            return Result.Failure("Current password is incorrect.");

        var result = passwordService.HashPassword(command.NewPassword);
        target.PasswordHash = result.Hash;
        target.PasswordSalt = result.Salt;
        target.Touch();

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
