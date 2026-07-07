using DASHBOARD.Application.Backlog.DTOs;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Enums;
using MediatR;

namespace DASHBOARD.Application.Backlog.Commands.CreateBacklogItem;

/// <summary>Creates a new backlog item (Epic, Feature, or UserStory). Requires <see cref="SystemFunction.ManageBacklog"/>.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="Type">The hierarchy level.</param>
/// <param name="Title">Item title.</param>
/// <param name="ParentId">Parent backlog item ID (null for root-level items).</param>
/// <param name="StoryPoints">Optional Fibonacci estimate.</param>
/// <param name="TshirtSize">Optional T-shirt size estimate.</param>
/// <param name="AcceptanceCriteria">Acceptance criteria text.</param>
public sealed record CreateBacklogItemCommand(
    Guid             RepositoryId,
    BacklogItemType  Type,
    string           Title,
    Guid?            ParentId,
    int?             StoryPoints,
    TshirtSize?      TshirtSize,
    string           AcceptanceCriteria) : IRequest<Result<BacklogItemDto>>;
