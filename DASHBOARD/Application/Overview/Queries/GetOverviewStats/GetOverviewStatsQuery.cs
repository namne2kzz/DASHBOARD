using DASHBOARD.Application.Overview.DTOs;
using MediatR;

namespace DASHBOARD.Application.Overview.Queries.GetOverviewStats;

/// <summary>Returns aggregated overview statistics for the given repository and sprint.</summary>
/// <param name="RepositoryId">The repository to query.</param>
/// <param name="SprintId">The sprint to focus on; null selects the active sprint or the most recent one.</param>
public sealed record GetOverviewStatsQuery(Guid RepositoryId, Guid? SprintId) : IRequest<OverviewStatsDto>;
