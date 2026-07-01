using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Enums;
using MediatR;

namespace DASHBOARD.Application.Backlog.Commands.UpdateBacklogItem;

/// <summary>Updates the fields of a backlog item. Requires <see cref="SystemFunction.EditWorkItem"/>.</summary>
public sealed record UpdateBacklogItemCommand(
    Guid                  RepositoryId,
    Guid                  ItemId,
    string                Title,
    BacklogItemState      State,
    Guid?                 SprintId,
    int?                  StoryPoints,
    TshirtSize?           TshirtSize,
    string                AcceptanceCriteria,
    IReadOnlyList<string> Documents) : IRequest<Result>;
