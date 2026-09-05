using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Infrastructure.Services;

/// <summary>
/// Merges <see cref="ICurrentUserService"/> identity with <see cref="IPermissionService"/> checks
/// so command handlers need only one dependency instead of two.
/// </summary>
public sealed class RequestUserContext(
    ICurrentUserService currentUser,
    IPermissionService  permissions) : IRequestUserContext
{
    /// <inheritdoc/>
    public Guid UserId => currentUser.UserId
        ?? throw new InvalidOperationException("No authenticated user in context.");

    /// <inheritdoc/>
    public Guid OrgId => currentUser.OrgId
        ?? throw new InvalidOperationException("No authenticated organization in context.");

    /// <inheritdoc/>
    public Task<bool> IsGlobalAdminAsync(CancellationToken ct = default) =>
        permissions.IsGlobalAdminAsync(UserId, ct);

    /// <inheritdoc/>
    public Task<bool> IsMemberOfAsync(Guid repositoryId, CancellationToken ct = default) =>
        permissions.IsMemberAsync(UserId, repositoryId, ct);

    /// <inheritdoc/>
    public Task<bool> CanAsync(Guid repositoryId, SystemFunction privilege, CancellationToken ct = default) =>
        permissions.HasPrivilegeAsync(UserId, repositoryId, privilege, ct);

    /// <inheritdoc/>
    public bool IsSelf(Guid targetUserId) => UserId == targetUserId;

    /// <inheritdoc/>
    public async Task<bool> CanManageUserAsync(Guid targetUserId, CancellationToken ct = default)
    {
        if (IsSelf(targetUserId)) return false;
        return await IsGlobalAdminAsync(ct);
    }
}
