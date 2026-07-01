using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.SmartBoard.Commands.ReorderColumns;

/// <summary>Reorders all board columns by assigning new sequential Order values based on the supplied ID list.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="OrderedColumnIds">All column IDs in the desired order (must include every column in the repository).</param>
public sealed record ReorderColumnsCommand(
    Guid             RepositoryId,
    List<Guid>       OrderedColumnIds) : IRequest<Result>;
