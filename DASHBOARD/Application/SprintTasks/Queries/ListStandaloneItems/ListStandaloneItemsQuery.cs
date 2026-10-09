using DASHBOARD.Application.SprintTasks.DTOs;
using DASHBOARD.Domain.Enums;
using MediatR;

namespace DASHBOARD.Application.SprintTasks.Queries.ListStandaloneItems;

/// <summary>Returns standalone work items (Bug/TestPlan) not yet assigned to a sprint.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="Type">Optional type filter.</param>
/// <param name="State">Optional state filter.</param>
public sealed record ListStandaloneItemsQuery(
    Guid            RepositoryId,
    SprintTaskType? Type  = null,
    WorkItemState?  State = null) : IRequest<IReadOnlyList<SprintTaskSummaryDto>>;
