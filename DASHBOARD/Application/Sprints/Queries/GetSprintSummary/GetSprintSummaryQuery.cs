using DASHBOARD.Application.Sprints.DTOs;
using MediatR;

namespace DASHBOARD.Application.Sprints.Queries.GetSprintSummary;

/// <summary>Returns velocity and capacity metrics for a sprint.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintId">The sprint to summarise.</param>
public sealed record GetSprintSummaryQuery(Guid RepositoryId, Guid SprintId) : IRequest<SprintSummaryDto>;
