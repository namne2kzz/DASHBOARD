using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Application.Roles.DTOs;

/// <summary>Snapshot of a role (global default or repository-scoped custom) including its permission set.</summary>
/// <param name="Id">Role identifier.</param>
/// <param name="RepositoryId">Owning repository, or null for global default roles.</param>
/// <param name="IsDefault">True for seeded, non-editable default roles.</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">Purpose description.</param>
/// <param name="Permissions">Allowed system functions.</param>
/// <param name="MemberCount">Number of members assigned to this role.</param>
public sealed record RoleDto(
    Guid                          Id,
    Guid?                         RepositoryId,
    bool                          IsDefault,
    string                        Name,
    string                        Description,
    IReadOnlyList<SystemFunction> Permissions,
    int                           MemberCount);
