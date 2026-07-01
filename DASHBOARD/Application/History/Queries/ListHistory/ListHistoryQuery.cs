using DASHBOARD.Application.History.DTOs;
using MediatR;

namespace DASHBOARD.Application.History.Queries.ListHistory;

/// <summary>Lists audit-trail entries for a sprint task, newest first.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintTaskId">The sprint task to list history for.</param>
public sealed record ListHistoryQuery(
    Guid RepositoryId,
    Guid SprintTaskId) : IRequest<IReadOnlyList<HistoryDto>>;
