using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Capacity.Commands.RemoveCapacityMember;

/// <summary>Removes a team member's capacity row from a sprint.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintId">The sprint.</param>
/// <param name="CapacityMemberId">The capacity row to remove.</param>
public sealed record RemoveCapacityMemberCommand(Guid RepositoryId, Guid SprintId, Guid CapacityMemberId) : IRequest<Result>;
