using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Application.Common.Interfaces;

/// <summary>
/// Evaluates whether a user may perform a specific action within a repository.
/// <para>
/// Privilege resolution order:
/// <list type="number">
///   <item>GlobalAdmin → all privileges granted unconditionally.</item>
///   <item>Otherwise → exactly the permissions of the member's assigned <see cref="Role"/> (its AllowedFunctions).</item>
/// </list>
/// The team role (<c>DefaultRole</c>) is a discipline label only and grants no permissions.
/// </para>
/// </summary>
public interface IPermissionService
{
    /// <summary>Returns <c>true</c> when the user holds the global-admin flag.</summary>
    /// <param name="userId">The user to evaluate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if the user is a global admin; otherwise <c>false</c>.</returns>
    Task<bool> IsGlobalAdminAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Returns <c>true</c> when the user is a member of the repository (or is a global admin).</summary>
    /// <param name="userId">The user to evaluate.</param>
    /// <param name="repositoryId">The target repository.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if the user may access the repository; otherwise <c>false</c>.</returns>
    Task<bool> IsMemberAsync(Guid userId, Guid repositoryId, CancellationToken ct = default);

    /// <summary>
    /// Returns <c>true</c> when the user holds the specified privilege in the repository.
    /// Privilege = the assigned Role's AllowedFunctions (global admins bypass).
    /// </summary>
    /// <param name="userId">The user to evaluate.</param>
    /// <param name="repositoryId">The target repository.</param>
    /// <param name="privilege">The privilege to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if the user has the privilege; otherwise <c>false</c>.</returns>
    Task<bool> HasPrivilegeAsync(Guid userId, Guid repositoryId, SystemFunction privilege, CancellationToken ct = default);

    /// <summary>Returns the full set of effective privileges for the user in the repository (role defaults ∪ custom extras).</summary>
    /// <param name="userId">The user to evaluate.</param>
    /// <param name="repositoryId">The target repository.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The union of role-default and custom-role privileges, or an empty set when not a member.</returns>
    Task<IReadOnlySet<SystemFunction>> GetPrivilegesAsync(Guid userId, Guid repositoryId, CancellationToken ct = default);

    /// <summary>Loads the membership row for the user + repository, including the custom role navigation. Returns <c>null</c> when not a member.</summary>
    /// <param name="userId">The user to evaluate.</param>
    /// <param name="repositoryId">The target repository.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <see cref="RepositoryMember"/> or <c>null</c>.</returns>
    Task<RepositoryMember?> GetMembershipAsync(Guid userId, Guid repositoryId, CancellationToken ct = default);
}
