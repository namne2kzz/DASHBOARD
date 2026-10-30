using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Common.Guards;

/// <summary>
/// Shared invariant: a repository must always retain at least one member who can lead it — that
/// is, whose assigned <see cref="Role.AllowedFunctions"/> grants <see cref="SystemFunction.ManageMembers"/>.
/// </summary>
/// <remarks>
/// Evaluated by actual permission, never by role or discipline name (per roles.dod.md §3, and the
/// fix for BUG-004). A repository may rename its lead role, or grant the same authority through a
/// custom role; what matters is that somebody can still run the place, not what their title says.
/// <para>
/// The default lead role is Project Manager, which carries every permission, so a repository with
/// a Project Manager always satisfies this. It is not the only way to satisfy it.
/// </para>
/// </remarks>
public static class RepositoryLeadGuard
{
    /// <summary>
    /// Reports whether a change would leave the repository with nobody able to manage members.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="repositoryId">The repository being checked.</param>
    /// <param name="excludedMemberId">The member being removed or changed; excluded from the "before" headcount.</param>
    /// <param name="overrideRoleId">
    /// When the member is being reassigned rather than removed, the role they would end up
    /// holding; counted back in with its own permissions.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when the change would leave the repository with nobody able to lead it.</returns>
    public static async Task<bool> WouldOrphanLeadAsync(
        IApplicationDbContext db,
        Guid repositoryId,
        Guid excludedMemberId,
        Guid? overrideRoleId,
        CancellationToken ct)
    {
        var others = await db.Set<RepositoryMember>()
            .AsNoTracking()
            .Include(m => m.Role)
            .Where(m => m.RepositoryId == repositoryId && m.Id != excludedMemberId)
            .ToListAsync(ct);

        if (others.Any(m => m.Role!.AllowedFunctions.Contains(SystemFunction.ManageMembers)))
            return false;

        if (overrideRoleId is null)
            return true;

        var overrideRole = await db.Set<Role>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == overrideRoleId, ct);
        return overrideRole is null || !overrideRole.AllowedFunctions.Contains(SystemFunction.ManageMembers);
    }
}
