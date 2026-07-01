using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Backlog.Commands.PromoteToSprint;

/// <summary>Promotes a backlog UserStory to the active sprint by creating a <see cref="DASHBOARD.Domain.Entities.SprintTask"/> row and marking the backlog item as Committed.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="ItemId">The backlog item to promote (must be type UserStory and state Ready).</param>
/// <param name="SprintId">The target sprint.</param>
/// <returns>The ID of the newly created <see cref="DASHBOARD.Domain.Entities.SprintTask"/>.</returns>
public sealed record PromoteToSprintCommand(
    Guid RepositoryId,
    Guid ItemId,
    Guid SprintId) : IRequest<Result<Guid>>;
