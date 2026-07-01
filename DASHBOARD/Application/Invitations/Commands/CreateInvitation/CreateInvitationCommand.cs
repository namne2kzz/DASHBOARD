using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Invitations.DTOs;
using MediatR;

namespace DASHBOARD.Application.Invitations.Commands.CreateInvitation;

/// <summary>Creates a repository invitation for an external email and dispatches the invite email asynchronously.</summary>
/// <param name="RepositoryId">The repository the invitee will join upon acceptance.</param>
/// <param name="Email">Email address to invite. Must not already exist in the system.</param>
public sealed record CreateInvitationCommand(
    Guid RepositoryId,
    string Email) : IRequest<Result<InvitationDto>>;
