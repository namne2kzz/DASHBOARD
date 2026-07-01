using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Backlog.Commands.UpdateBacklogAcceptanceCriteria;

/// <summary>Updates the acceptance criteria text of a backlog item.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="ItemId">The backlog item to update.</param>
/// <param name="AcceptanceCriteria">New acceptance criteria text.</param>
public sealed record UpdateBacklogAcceptanceCriteriaCommand(
    Guid   RepositoryId,
    Guid   ItemId,
    string AcceptanceCriteria) : IRequest<Result>;
