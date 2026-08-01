namespace DASHBOARD.Controllers.Users.Requests;

/// <summary>HTTP request body for creating a new system user account.</summary>
/// <param name="Name">Display name for the new user.</param>
/// <param name="Email">Unique email used as the login identifier.</param>
/// <param name="Password">Plaintext password (min 8 chars) — hashed before storage.</param>
/// <param name="IsGlobalAdmin">Whether the new account should have global-admin privileges.</param>
/// <param name="AvatarClass">Tailwind CSS background color class for the avatar (e.g. "bg-sky-600"), chosen by the client.</param>
/// <param name="ManagerId">Optional manager for the new user in the organisation hierarchy.</param>
public sealed record CreateUserRequest(
    string Name,
    string Email,
    string Password,
    bool   IsGlobalAdmin,
    string AvatarClass,
    Guid?  ManagerId = null);
