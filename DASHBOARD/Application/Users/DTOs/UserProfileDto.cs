namespace DASHBOARD.Application.Users.DTOs;

/// <summary>Public profile snapshot of an application user.</summary>
public sealed record UserProfileDto(
    Guid   Id,
    string Name,
    string Email,
    string AvatarClass,
    bool   IsGlobalAdmin,
    Guid?  ManagerId);
