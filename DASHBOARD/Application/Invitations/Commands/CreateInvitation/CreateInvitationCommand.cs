using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Invitations.DTOs;
using MediatR;

namespace DASHBOARD.Application.Invitations.Commands.CreateInvitation;

/// <summary>Creates a repository invitation for an external email and dispatches the invite email asynchronously.</summary>
/// <param name="RepositoryId">The repository the invitee will join upon acceptance.</param>
/// <param name="Email">Email address to invite. Must not already exist in the system.</param>
/// <param name="DefaultRole">The team role (discipline) the invitee will be assigned on acceptance.</param>
/// <param name="RoleId">The role (default or custom) granting permissions, applied on acceptance. Required.</param>
/// <param name="ManagerId">Optional manager the invitee's new account will report to, applied when a new user is created on acceptance.</param>
public sealed record CreateInvitationCommand(
    Guid RepositoryId,
    string Email,
    string DefaultRole,
    Guid RoleId,
    Guid? ManagerId = null) : IRequest<Result<InvitationDto>>;
