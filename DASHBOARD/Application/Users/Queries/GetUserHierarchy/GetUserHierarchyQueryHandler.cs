using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Users.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Users.Queries.GetUserHierarchy;

/// <summary>Handles <see cref="GetUserHierarchyQuery"/>: builds the manager/peer/report slice for a user. Global admins only.</summary>
public sealed class GetUserHierarchyQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<GetUserHierarchyQuery, UserHierarchyDto>
{
    /// <summary>Loads every user once, then derives the ancestors, manager, peers, and direct reports of the focus user.</summary>
    /// <param name="query">The hierarchy query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The organisation slice centred on the focus user.</returns>
    public async Task<UserHierarchyDto> Handle(GetUserHierarchyQuery query, CancellationToken ct)
    {
        if (!await user.IsGlobalAdminAsync(ct))
            throw new UnauthorizedAccessException("Only global admins may view the organisation hierarchy.");

        var all = await db.Set<User>().IgnoreQueryFilters().AsNoTracking()
            .Select(u => new { u.Id, u.Name, u.Email, u.AvatarClass, u.IsGlobalAdmin, u.IsDeleted, u.ManagerId })
            .ToListAsync(ct);

        var byId = all.ToDictionary(u => u.Id);
        if (!byId.TryGetValue(query.UserId, out var self))
            throw new NotFoundException(nameof(User), query.UserId);

        var directReportCount = all
            .Where(u => u.ManagerId.HasValue)
            .GroupBy(u => u.ManagerId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        UserNodeDto Node(Guid id)
        {
            var u = byId[id];
            return new UserNodeDto(u.Id, u.Name, u.Email, u.AvatarClass, u.IsGlobalAdmin, !u.IsDeleted,
                directReportCount.GetValueOrDefault(u.Id));
        }

        // Ancestors: walk up from the direct manager to the root, then reverse to top-down order.
        var ancestors = new List<UserNodeDto>();
        var cursor = self.ManagerId;
        var guard = 0;
        while (cursor is { } id && byId.ContainsKey(id) && guard++ < 100)
        {
            ancestors.Add(Node(id));
            cursor = byId[id].ManagerId;
        }
        ancestors.Reverse();

        var manager = self.ManagerId is { } mid && byId.ContainsKey(mid) ? Node(mid) : null;

        var peers = all
            .Where(u => u.Id != self.Id && u.ManagerId == self.ManagerId)
            .OrderBy(u => u.Name)
            .Select(u => Node(u.Id))
            .ToList();

        var subordinates = all
            .Where(u => u.ManagerId == self.Id)
            .OrderBy(u => u.Name)
            .Select(u => Node(u.Id))
            .ToList();

        return new UserHierarchyDto(ancestors, manager, Node(self.Id), peers, subordinates);
    }
}
