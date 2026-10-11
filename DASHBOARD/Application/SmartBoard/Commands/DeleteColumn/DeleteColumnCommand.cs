using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.SmartBoard.Commands.DeleteColumn;

/// <summary>Deletes a board column's configuration. Does not affect work items — the board maps tasks by state.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="ColumnId">The column to delete.</param>
public sealed record DeleteColumnCommand(
    Guid RepositoryId,
    Guid ColumnId) : IRequest<Result>;
