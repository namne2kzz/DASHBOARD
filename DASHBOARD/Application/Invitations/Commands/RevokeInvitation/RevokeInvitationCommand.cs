using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Invitations.Commands.RevokeInvitation;

/// <summary>Revokes a still-pending invitation, e.g. one sent by mistake or no longer needed.</summary>
/// <param name="RepositoryId">The repository the invitation belongs to.</param>
/// <param name="InvitationId">The invitation to revoke.</param>
public sealed record RevokeInvitationCommand(Guid RepositoryId, Guid InvitationId) : IRequest<Result>;
