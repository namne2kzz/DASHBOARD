using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Users.Commands.SetUserManager;

/// <summary>
/// Handles <see cref="SetUserManagerCommand"/>: assigns a user's manager after validating admin
/// permission, self-reference, existence, and that the change introduces no cycle in the hierarchy.
/// </summary>
public sealed class SetUserManagerCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<SetUserManagerCommand, Result>
{
    /// <summary>Validates the assignment and persists the new manager link.</summary>
    /// <param name="command">The set-manager command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success; failure on permission or validation error.</returns>
    public async Task<Result> Handle(SetUserManagerCommand command, CancellationToken ct)
    {
        if (!await user.IsGlobalAdminAsync(ct))
            throw new ForbiddenException("Only global admins may change the organisation hierarchy.");

        if (command.ManagerId == command.UserId)
            return Result.Failure("A user cannot be their own manager.");

        var target = await db.Set<User>().IgnoreQueryFilters().AsTracking()
            .FirstOrDefaultAsync(u => u.Id == command.UserId, ct)
            ?? throw new NotFoundException(nameof(User), command.UserId);

        if (command.ManagerId is { } managerId)
        {
            // Load the id → managerId map once to walk the chain and detect cycles.
            var chain = await db.Set<User>().IgnoreQueryFilters().AsNoTracking()
                .Select(u => new { u.Id, u.ManagerId })
                .ToDictionaryAsync(u => u.Id, u => u.ManagerId, ct);

            if (!chain.ContainsKey(managerId))
                return Result.Failure("The selected manager does not exist.");

            // Walk up from the proposed manager; reaching the target means this would form a cycle.
            var cursor = (Guid?)managerId;
            while (cursor is { } current)
            {
                if (current == command.UserId)
                    return Result.Failure("That assignment would create a cycle in the hierarchy.");
                cursor = chain.TryGetValue(current, out var next) ? next : null;
            }
        }

        target.ManagerId = command.ManagerId;
        target.Touch();

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
