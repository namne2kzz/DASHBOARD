using DASHBOARD.Application.Users.DTOs;
using MediatR;

namespace DASHBOARD.Application.Users.Commands.CreateUser;

/// <summary>Creates a new system user account. Restricted to global admins.</summary>
/// <param name="Name">Display name for the new user.</param>
/// <param name="Email">Unique email address used as the login identifier.</param>
/// <param name="Password">Plaintext password — will be hashed before storage.</param>
/// <param name="IsGlobalAdmin">Whether the new user should have global-admin privileges.</param>
/// <param name="AvatarClass">Tailwind CSS background color class for the avatar (e.g. "bg-sky-600"), chosen by the caller.</param>
/// <param name="ManagerId">Optional manager for the new user in the organisation hierarchy.</param>
public sealed record CreateUserCommand(
    string Name,
    string Email,
    string Password,
    bool   IsGlobalAdmin,
    string AvatarClass,
    Guid?  ManagerId = null) : IRequest<SystemUserListItemDto>;
