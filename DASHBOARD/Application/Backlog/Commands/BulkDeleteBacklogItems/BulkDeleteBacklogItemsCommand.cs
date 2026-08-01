using DASHBOARD.Application.Backlog.DTOs;
using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Backlog.Commands.BulkDeleteBacklogItems;

/// <summary>Deletes multiple backlog items in a single operation. Items whose children are not also selected are skipped to avoid orphaning.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="ItemIds">The backlog items to delete.</param>
public sealed record BulkDeleteBacklogItemsCommand(
    Guid                RepositoryId,
    IReadOnlyList<Guid> ItemIds) : IRequest<Result<BulkOperationResultDto>>;
