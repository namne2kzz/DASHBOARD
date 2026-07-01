using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Application.Common.Interfaces;

/// <summary>
/// Provides identity and permission checks for the currently authenticated request user,
/// eliminating the need for handlers to juggle both <see cref="ICurrentUserService"/> and
/// <see cref="IPermissionService"/> separately.
/// </summary>
public interface IRequestUserContext
{
    /// <summary>Gets the authenticated user's ID. Throws <see cref="InvalidOperationException"/> when not authenticated.</summary>
    Guid UserId { get; }

    /// <summary>Returns <c>true</c> when the user holds the global-admin flag.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> IsGlobalAdminAsync(CancellationToken ct = default);

    /// <summary>Returns <c>true</c> when the user is a member of the repository (global admins pass automatically).</summary>
    /// <param name="repositoryId">Target repository.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> IsMemberOfAsync(Guid repositoryId, CancellationToken ct = default);

    /// <summary>Returns <c>true</c> when the user holds the specified privilege in the repository (global admins pass automatically).</summary>
    /// <param name="repositoryId">Target repository.</param>
    /// <param name="privilege">The privilege to evaluate.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> CanAsync(Guid repositoryId, SystemFunction privilege, CancellationToken ct = default);

    /// <summary>Returns <c>true</c> when <paramref name="targetUserId"/> equals the authenticated user's ID.</summary>
    /// <param name="targetUserId">The ID to compare against.</param>
    bool IsSelf(Guid targetUserId);

    /// <summary>
    /// Returns <c>true</c> when the user is a global admin and the target is not themselves.
    /// Use for system-level user management actions (activate, deactivate, etc.).
    /// </summary>
    /// <param name="targetUserId">ID of the system user being acted on.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> CanManageUserAsync(Guid targetUserId, CancellationToken ct = default);
}
