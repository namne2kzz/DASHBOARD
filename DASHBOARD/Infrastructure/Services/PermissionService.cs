using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Infrastructure.Services;

/// <summary>
/// Evaluates repository-level privileges. Permissions come solely from the member's assigned
/// <see cref="Role"/> (its <see cref="Role.AllowedFunctions"/>); global admins bypass all checks.
/// The member's <see cref="RepositoryMember.DefaultRole"/> (team role) is a discipline label only and
/// does not grant any permission.
/// </summary>
public sealed class PermissionService(
    IApplicationDbContext  db) : IPermissionService
{
    /// <inheritdoc/>
    public async Task<bool> IsGlobalAdminAsync(Guid userId, CancellationToken ct = default) =>
        await db.Set<User>().AsNoTracking().AnyAsync(u => u.Id == userId && u.IsGlobalAdmin, ct);

    /// <inheritdoc/>
    public async Task<bool> IsMemberAsync(Guid userId, Guid repositoryId, CancellationToken ct = default)
    {
        if (await IsGlobalAdminAsync(userId, ct)) return true;
        return await db.Set<RepositoryMember>()
            .AsNoTracking()
            .AnyAsync(m => m.UserId == userId && m.RepositoryId == repositoryId, ct);
    }

    /// <inheritdoc/>
    public async Task<bool> HasPrivilegeAsync(Guid userId, Guid repositoryId, SystemFunction privilege, CancellationToken ct = default)
    {
        if (await IsGlobalAdminAsync(userId, ct)) return true;

        var effective = await GetPrivilegesAsync(userId, repositoryId, ct);
        return effective.Contains(privilege);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlySet<SystemFunction>> GetPrivilegesAsync(Guid userId, Guid repositoryId, CancellationToken ct = default)
    {
        var member = await GetMembershipAsync(userId, repositoryId, ct);
        if (member is null) return new HashSet<SystemFunction>();

        // Permissions come solely from the assigned Role.
        return member.Role is not null
            ? new HashSet<SystemFunction>(member.Role.AllowedFunctions)
            : new HashSet<SystemFunction>();
    }

    /// <inheritdoc/>
    public async Task<RepositoryMember?> GetMembershipAsync(Guid userId, Guid repositoryId, CancellationToken ct = default) =>
        await db.Set<RepositoryMember>()
            .AsNoTracking()
            .Include(m => m.Role)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.RepositoryId == repositoryId, ct);
}
