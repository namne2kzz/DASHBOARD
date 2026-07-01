using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Members.Commands.RemoveMember;

/// <summary>Removes a user from a repository. Fails if they are the last ScrumMaster.</summary>
/// <param name="RepositoryId">The repository to remove the member from.</param>
/// <param name="MemberId">The membership row to remove.</param>
public sealed record RemoveMemberCommand(Guid RepositoryId, Guid MemberId) : IRequest<Result>;
