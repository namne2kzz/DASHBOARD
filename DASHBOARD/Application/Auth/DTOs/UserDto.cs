namespace DASHBOARD.Application.Auth.DTOs;

/// <summary>Public representation of an authenticated user returned from <c>GET /api/auth/me</c>.</summary>
/// <param name="UserId">The user's unique identifier.</param>
/// <param name="Email">The user's email address.</param>
/// <param name="Name">The user's display name.</param>
/// <param name="AvatarClass">Tailwind CSS background class for the avatar chip.</param>
/// <param name="IsGlobalAdmin">Whether the user has system-wide admin access.</param>
public record UserDto(
    Guid UserId,
    string Email,
    string Name,
    string AvatarClass,
    bool IsGlobalAdmin);
