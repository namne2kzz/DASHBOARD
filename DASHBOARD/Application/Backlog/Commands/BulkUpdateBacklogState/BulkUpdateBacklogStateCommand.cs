using DASHBOARD.Application.Backlog.DTOs;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Enums;
using MediatR;

namespace DASHBOARD.Application.Backlog.Commands.BulkUpdateBacklogState;

/// <summary>Transitions multiple backlog items to the same refinement state in a single operation.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="ItemIds">The backlog items to update.</param>
/// <param name="State">Target refinement state (New, Refining, or Ready). Committed is rejected.</param>
public sealed record BulkUpdateBacklogStateCommand(
    Guid                  RepositoryId,
    IReadOnlyList<Guid>   ItemIds,
    BacklogItemState      State) : IRequest<Result<BulkOperationResultDto>>;
