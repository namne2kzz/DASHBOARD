using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Sprints.Commands.DeleteSprint;

/// <summary>Deletes a sprint. Blocked if the sprint has committed SprintTask rows.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintId">The sprint to delete.</param>
public sealed record DeleteSprintCommand(Guid RepositoryId, Guid SprintId) : IRequest<Result>;
