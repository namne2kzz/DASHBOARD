using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Capacity.DTOs;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Capacity.Queries.GetCapacity;

/// <summary>Handles <see cref="GetCapacityQuery"/>: loads members and day-off entries with user details.</summary>
public sealed class GetCapacityQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<GetCapacityQuery, GetCapacityResult>
{
    /// <summary>Validates membership, loads capacity and day-off rows, and returns the combined result.</summary>
    /// <param name="query">The get query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="GetCapacityResult"/> with members and days off.</returns>
    public async Task<GetCapacityResult> Handle(GetCapacityQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        var members = await db.Set<CapacityMember>()
            .AsNoTracking()
            .Include(c => c.User)
            .Where(c => c.SprintId == query.SprintId)
            .OrderBy(c => c.User!.Name)
            .Select(c => new CapacityMemberDto(c.Id, c.SprintId, c.UserId, c.User!.Name, c.User.AvatarClass,
                c.Role, c.HoursPerDay, c.OvertimeHoursPerDay))
            .ToListAsync(ct);

        var daysOff = await db.Set<DayOff>()
            .AsNoTracking()
            .Include(d => d.User)
            .Where(d => d.SprintId == query.SprintId)
            .OrderBy(d => d.Date)
            .Select(d => new DayOffDto(d.Id, d.SprintId, d.UserId, d.User != null ? d.User.Name : null,
                d.Date, d.Hours, d.Reason))
            .ToListAsync(ct);

        return new GetCapacityResult(members, daysOff);
    }
}
