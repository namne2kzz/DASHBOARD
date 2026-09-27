using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Users.Commands.ToggleAdmin
{
    /// <summary>Handles <see cref="ToggleAdminCommand"/>: enforces admin-only rule then persists the IsGlobalAdmin changes.</summary>
    public sealed class ToggleAdminCommandHandler(
        IApplicationDbContext db,
        IRequestUserContext   user,
        IUnitOfWork           uow) : IRequestHandler<ToggleAdminCommand, Result>
    {
        /// <summary>Validates requester identity, applies the global admin role, and commits.</summary>
        /// <param name="command">The update request.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns><see cref="Result.Ok"/> on success; <see cref="Result.Failure"/> on business-rule violation.</returns>
        public async Task<Result> Handle(ToggleAdminCommand command, CancellationToken ct)
        {
            // Only another global admin may toggle a user's global admin status — never self.
            if (user.IsSelf(command.TargetUserId) || !await user.IsGlobalAdminAsync(ct))
                throw new ForbiddenException("You do not have permission to toggle to global admin.");

            var target = await db.Set<User>().AsTracking()
                .FirstOrDefaultAsync(u => u.Id == command.TargetUserId, ct)
                ?? throw new NotFoundException(nameof(User), command.TargetUserId);

            target.IsGlobalAdmin = !target.IsGlobalAdmin;
            target.Touch();

            await uow.CommitAsync(ct);
            return Result.Ok;
        }
    }
}
