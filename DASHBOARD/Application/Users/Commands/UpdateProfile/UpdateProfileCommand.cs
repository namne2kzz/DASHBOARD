using MediatR;
using DASHBOARD.Application.Common.Models;

namespace DASHBOARD.Application.Users.Commands.UpdateProfile;

/// <summary>Updates the display name and avatar class of an application user. Only the user themselves or a global admin may call this.</summary>
/// <param name="TargetUserId">The ID of the user whose profile will be updated.</param>
/// <param name="Name">New display name (1–100 chars).</param>
/// <param name="AvatarClass">New Tailwind CSS background class (e.g. "bg-sky-600").</param>
public sealed record UpdateProfileCommand(
    Guid   TargetUserId,
    string Name,
    string AvatarClass) : IRequest<Result>;
