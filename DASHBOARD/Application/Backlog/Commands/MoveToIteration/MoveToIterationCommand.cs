using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Backlog.Commands.MoveToIteration;

/// <summary>Assigns (or clears) the sprint for a backlog item, transitioning its state accordingly.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="ItemId">The backlog item to update.</param>
/// <param name="SprintId">The sprint to assign, or <c>null</c> to unschedule.</param>
public sealed record MoveToIterationCommand(
    Guid  RepositoryId,
    Guid  ItemId,
    Guid? SprintId) : IRequest<Result>;
