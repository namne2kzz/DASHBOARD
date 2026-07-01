using MediatR;
using DASHBOARD.Application.Common.Models;

namespace DASHBOARD.Application.Users.Commands.ChangePassword;

/// <summary>Changes the authenticated user's password after verifying the current one. Only the user themselves may call this (not even global admins).</summary>
/// <param name="TargetUserId">The user whose password is being changed.</param>
/// <param name="OldPassword">Current plaintext password for verification.</param>
/// <param name="NewPassword">The desired new plaintext password.</param>
public sealed record ChangePasswordCommand(
    Guid   TargetUserId,
    string OldPassword,
    string NewPassword) : IRequest<Result>;
