using DASHBOARD.Application.Backlog.DTOs;
using DASHBOARD.Domain.Enums;
using MediatR;

namespace DASHBOARD.Application.Backlog.Queries.ListBacklogItems;

/// <summary>Returns the backlog tree for a repository. Root items include their children recursively.</summary>
/// <param name="RepositoryId">The repository to query.</param>
/// <param name="TypeFilter">Optional filter by item type.</param>
/// <param name="StateFilter">Optional filter by refinement state.</param>
public sealed record ListBacklogItemsQuery(
    Guid             RepositoryId,
    BacklogItemType? TypeFilter  = null,
    BacklogItemState? StateFilter = null) : IRequest<IReadOnlyList<BacklogItemDto>>;
