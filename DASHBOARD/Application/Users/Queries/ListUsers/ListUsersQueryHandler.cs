using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Users.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Users.Queries.ListUsers;

/// <summary>Handles <see cref="ListUsersQuery"/>: enforces global-admin gate then returns a paged, optionally filtered list of users with their repository memberships.</summary>
public sealed class ListUsersQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<ListUsersQuery, PagedResult<SystemUserListItemDto>>
{
    /// <summary>Checks global-admin permission, applies optional search filter, and returns a paged result with membership data.</summary>
    /// <param name="query">The list query with optional search and pagination.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="PagedResult{T}"/> of <see cref="SystemUserListItemDto"/>.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the caller is not a global admin.</exception>
    public async Task<PagedResult<SystemUserListItemDto>> Handle(ListUsersQuery query, CancellationToken ct)
    {
        if (!await user.IsGlobalAdminAsync(ct))
            throw new UnauthorizedAccessException("Only global admins may list all users.");

        // Multi-tenant: an org admin only sees users within their own organization.
        var orgId = user.OrgId;
        var q = db.Set<User>().IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.OrgId == orgId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            q = q.Where(u => u.Name.ToLower().Contains(term) || u.Email.ToLower().Contains(term));
        }

        var total = await q.CountAsync(ct);

        var users = await q
            .OrderBy(u => u.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.Email,
                u.AvatarClass,
                u.IsGlobalAdmin,
                u.IsDeleted,
                u.AuthProvider,
                u.CreatedAt,
                u.ManagerId,
            })
            .ToListAsync(ct);

        if (users.Count == 0)
            return new PagedResult<SystemUserListItemDto>([], total, query.Page, query.PageSize);

        var userIds = users.Select(u => u.Id).ToList();

        // Resolve manager display names (a manager may not be on the current page).
        var managerIds = users.Where(u => u.ManagerId.HasValue).Select(u => u.ManagerId!.Value).Distinct().ToList();
        var managerNames = managerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Set<User>().IgnoreQueryFilters().AsNoTracking()
                .Where(u => managerIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Name })
                .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        var memberships = await db.Set<RepositoryMember>()
            .AsNoTracking()
            .Where(m => userIds.Contains(m.UserId))
            .Select(m => new
            {
                m.UserId,
                m.RepositoryId,
                RepoName       = m.Repository!.Name,
                RepoCode       = m.Repository!.Code,
                m.DefaultRole,
                RoleName       = m.Role != null ? m.Role.Name : null,
                JoinedAt       = m.CreatedAt,
            })
            .ToListAsync(ct);

        var membershipLookup = memberships
            .GroupBy(m => m.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var items = users.Select(u => new SystemUserListItemDto(
            UserId:          u.Id,
            Name:            u.Name,
            Email:           u.Email,
            AvatarClass:     u.AvatarClass,
            IsGlobalAdmin:   u.IsGlobalAdmin,
            IsActive:        !u.IsDeleted,
            AuthProvider:    u.AuthProvider,
            CreatedAt:       u.CreatedAt,
            LastLoginAt:     null,
            ManagerId:       u.ManagerId,
            ManagerName:     u.ManagerId.HasValue && managerNames.TryGetValue(u.ManagerId.Value, out var mn) ? mn : null,
            RepoMemberships: membershipLookup.TryGetValue(u.Id, out var mems)
                ? mems.Select(m => new UserRepoMembershipDto(
                    m.RepositoryId, m.RepoName, m.RepoCode, m.DefaultRole, m.RoleName, m.JoinedAt)).ToList()
                : []
        )).ToList();

        return new PagedResult<SystemUserListItemDto>(items, total, query.Page, query.PageSize);
    }
}
