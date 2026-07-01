using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Backlog.Commands.DeleteBacklogItem;

/// <summary>Deletes a backlog item. Fails if the item has children (must delete children first).</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="ItemId">The item to delete.</param>
public sealed record DeleteBacklogItemCommand(Guid RepositoryId, Guid ItemId) : IRequest<Result>;
