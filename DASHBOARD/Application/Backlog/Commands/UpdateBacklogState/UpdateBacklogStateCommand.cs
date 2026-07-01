using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Enums;
using MediatR;

namespace DASHBOARD.Application.Backlog.Commands.UpdateBacklogState;

/// <summary>Transitions a backlog item to a new refinement state.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="ItemId">The backlog item to update.</param>
/// <param name="State">Target refinement state (New, Refining, or Ready).</param>
public sealed record UpdateBacklogStateCommand(
    Guid             RepositoryId,
    Guid             ItemId,
    BacklogItemState State) : IRequest<Result>;
