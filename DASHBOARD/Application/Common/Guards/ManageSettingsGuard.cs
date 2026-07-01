using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Common.Guards;

/// <summary>
/// Shared invariant: a repository must always retain at least one member whose assigned
/// <see cref="Role.AllowedFunctions"/> grants <see cref="SystemFunction.ManageSettings"/>.
/// Evaluated by actual permission, never by role/discipline name (per roles.dod.md §3).
/// </summary>
public static class ManageSettingsGuard
{
    /// <summary>
    /// Returns <c>true</c> when, after excluding <paramref name="excludedMemberId"/> and applying
    /// <paramref name="overrideRoleId"/> (if the member is being reassigned rather than removed),
    /// no member of the repository would still hold <see cref="SystemFunction.ManageSettings"/>.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="repositoryId">The repository being checked.</param>
    /// <param name="excludedMemberId">The member being removed or changed; excluded from the "before" headcount.</param>
    /// <param name="overrideRoleId">When the member is being reassigned (not removed), the role they would end up holding; counted back in with its own permissions.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if the change would leave the repository with nobody able to manage settings.</returns>
    public static async Task<bool> WouldOrphanManageSettingsAsync(
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

        if (others.Any(m => m.Role!.AllowedFunctions.Contains(SystemFunction.ManageSettings)))
            return false;

        if (overrideRoleId is null)
            return true;

        var overrideRole = await db.Set<Role>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == overrideRoleId, ct);
        return overrideRole is null || !overrideRole.AllowedFunctions.Contains(SystemFunction.ManageSettings);
    }
}
