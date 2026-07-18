using DASHBOARD.Application.Invitations.DTOs;
using MediatR;

namespace DASHBOARD.Application.Invitations.Queries.ListInvitations;

/// <summary>Returns every invitation ever sent for a repository, newest first.</summary>
/// <param name="RepositoryId">The repository to query.</param>
public sealed record ListInvitationsQuery(Guid RepositoryId) : IRequest<IReadOnlyList<InvitationListItemDto>>;
