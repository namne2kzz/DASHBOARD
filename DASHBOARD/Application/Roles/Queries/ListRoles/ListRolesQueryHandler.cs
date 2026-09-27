using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Roles.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Roles.Queries.ListRoles;

/// <summary>Handles <see cref="ListRolesQuery"/>: checks membership, loads global default roles plus the repo's custom roles with member counts.</summary>
public sealed class ListRolesQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<ListRolesQuery, IReadOnlyList<RoleDto>>
{
    /// <summary>Validates membership, projects roles with their member counts, and returns the list (defaults first).</summary>
    /// <param name="query">The list query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All <see cref="RoleDto"/> records, default roles first then custom roles by name.</returns>
    public async Task<IReadOnlyList<RoleDto>> Handle(ListRolesQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        var roles = await db.Set<Role>()
            .AsNoTracking()
            .Where(r => r.RepositoryId == query.RepositoryId)
            .OrderByDescending(r => r.IsDefault)
            .ThenBy(r => r.Name)
            .ToListAsync(ct);

        var roleIds = roles.Select(r => r.Id).ToList();
        var memberCounts = await db.Set<RepositoryMember>()
            .AsNoTracking()
            .Where(m => m.RepositoryId == query.RepositoryId && roleIds.Contains(m.RoleId))
            .GroupBy(m => m.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var countMap = memberCounts.ToDictionary(x => x.RoleId, x => x.Count);

        return roles.Select(r => new RoleDto(
            r.Id, r.RepositoryId, r.IsDefault, r.Name, r.Description, r.AllowedFunctions,
            countMap.GetValueOrDefault(r.Id, 0))).ToList();
    }
}
