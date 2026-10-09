using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Sprints.Commands.ActivateSprint;

/// <summary>Transitions a Planning sprint to Active status. Only one sprint may be Active per repository.</summary>
public sealed record ActivateSprintCommand(Guid RepositoryId, Guid SprintId) : IRequest<Result>;
