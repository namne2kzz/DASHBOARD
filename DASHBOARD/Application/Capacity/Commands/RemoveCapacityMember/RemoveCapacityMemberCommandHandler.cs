using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Capacity.Commands.RemoveCapacityMember;

/// <summary>Handles <see cref="RemoveCapacityMemberCommand"/>: checks ManageCapacity permission then removes the row.</summary>
public sealed class RemoveCapacityMemberCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<RemoveCapacityMemberCommand, Result>
{
    /// <summary>Validates permission and removes the capacity row.</summary>
    /// <param name="command">The remove command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success.</returns>
    public async Task<Result> Handle(RemoveCapacityMemberCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageCapacity, ct))
            return Result.Failure("You do not have permission to manage capacity in this repository.");

        var cap = await db.Set<CapacityMember>()
            .FirstOrDefaultAsync(c => c.Id == command.CapacityMemberId && c.SprintId == command.SprintId, ct)
            ?? throw new NotFoundException(nameof(CapacityMember), command.CapacityMemberId);

        db.Set<CapacityMember>().Remove(cap);
        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
