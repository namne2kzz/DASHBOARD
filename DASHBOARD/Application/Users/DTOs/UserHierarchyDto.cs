namespace DASHBOARD.Application.Users.DTOs;

/// <summary>A lightweight user node in the organisation hierarchy view.</summary>
/// <param name="Id">User identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="Email">Email address.</param>
/// <param name="AvatarClass">Avatar colour class.</param>
/// <param name="IsGlobalAdmin">Whether the user is a global admin.</param>
/// <param name="IsActive">Whether the account is active.</param>
/// <param name="SubordinateCount">Number of direct reports.</param>
public sealed record UserNodeDto(
    Guid   Id,
    string Name,
    string Email,
    string AvatarClass,
    bool   IsGlobalAdmin,
    bool   IsActive,
    int    SubordinateCount);

/// <summary>
/// A user-centred slice of the organisation chart: the upward chain of managers, the direct manager,
/// the user themselves, same-manager peers, and direct reports.
/// </summary>
/// <param name="Ancestors">Managers from the top root down to the direct manager (top-down order).</param>
/// <param name="Manager">The direct manager, or null when the user is a root.</param>
/// <param name="Self">The focus user.</param>
/// <param name="Peers">Other users sharing the same manager (excludes self).</param>
/// <param name="Subordinates">Users who report directly to the focus user.</param>
public sealed record UserHierarchyDto(
    IReadOnlyList<UserNodeDto> Ancestors,
    UserNodeDto?               Manager,
    UserNodeDto                Self,
    IReadOnlyList<UserNodeDto> Peers,
    IReadOnlyList<UserNodeDto> Subordinates);
