using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Application.Users.DTOs;

/// <summary>Full admin view of a user, including activity state and repository memberships.</summary>
public sealed record SystemUserListItemDto(
    Guid                               UserId,
    string                             Name,
    string                             Email,
    string                             AvatarClass,
    bool                               IsGlobalAdmin,
    bool                               IsActive,
    AuthProvider                       AuthProvider,
    DateTime                           CreatedAt,
    DateTime?                          LastLoginAt,
    IReadOnlyList<UserRepoMembershipDto> RepoMemberships);

/// <summary>A single repository membership entry nested inside <see cref="SystemUserListItemDto"/>.</summary>
public sealed record UserRepoMembershipDto(
    Guid     RepoId,
    string   RepoName,
    string   RepoCode,
    string   DefaultRole,
    string?  RoleName,
    DateTime JoinedAt);
