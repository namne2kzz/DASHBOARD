using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Backlog.Commands.RankBacklogItem;

/// <summary>Repositions a backlog item using fractional ranking (midpoint between previous and next item's rank). Re-normalizes automatically when gaps collapse below 0.001.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="ItemId">The item to reposition.</param>
/// <param name="PreviousItemId">The item that should appear immediately before the moved item, or <c>null</c> to move to the top.</param>
/// <param name="NextItemId">The item that should appear immediately after the moved item, or <c>null</c> to move to the bottom.</param>
public sealed record RankBacklogItemCommand(
    Guid  RepositoryId,
    Guid  ItemId,
    Guid? PreviousItemId,
    Guid? NextItemId) : IRequest<Result>;
