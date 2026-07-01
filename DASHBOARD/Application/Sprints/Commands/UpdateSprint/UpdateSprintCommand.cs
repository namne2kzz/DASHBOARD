using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Sprints.Commands.UpdateSprint;

/// <summary>Updates the name and date range of a sprint.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintId">The sprint to update.</param>
/// <param name="Name">New sprint display name.</param>
/// <param name="StartDate">New start date.</param>
/// <param name="EndDate">New end date.</param>
public sealed record UpdateSprintCommand(
    Guid     RepositoryId,
    Guid     SprintId,
    string   Name,
    DateOnly StartDate,
    DateOnly EndDate) : IRequest<Result>;
