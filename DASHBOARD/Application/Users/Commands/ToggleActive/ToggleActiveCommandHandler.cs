using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Users.Commands.ToggleActive
{
    /// <summary>Handles <see cref="ToggleActiveCommand"/>: enforces admin-only rule then toggles the IsDeleted soft-delete flag.</summary>
    public sealed class ToggleActiveCommandHandler(
        IApplicationDbContext db,
        IRequestUserContext   user,
        IUnitOfWork           uow) : IRequestHandler<ToggleActiveCommand, Result>
    {
        /// <summary>Validates requester is a global admin and not targeting themselves, then flips the active state.</summary>
        /// <param name="command">The toggle request.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns><see cref="Result.Ok"/> on success; <see cref="Result.Failure"/> on business-rule violation.</returns>
        public async Task<Result> Handle(ToggleActiveCommand command, CancellationToken ct)
        {
            if (!await user.CanManageUserAsync(command.TargetUserId, ct))
                throw new ForbiddenException("Only global admins may activate or deactivate user accounts.");

            var target = await db.Set<User>().IgnoreQueryFilters().AsTracking()
                .FirstOrDefaultAsync(u => u.Id == command.TargetUserId, ct)
                ?? throw new NotFoundException(nameof(User), command.TargetUserId);

            if (target.IsDeleted)
            {
                target.IsDeleted       = false;
                target.DeletedAt       = null;
                target.DeletedByUserId = null;
            }
            else
            {
                target.IsDeleted       = true;
                target.DeletedAt       = DateTime.UtcNow;
                target.DeletedByUserId = user.UserId;
            }

            target.Touch();
            await uow.CommitAsync(ct);
            return Result.Ok;
        }
    }
}
