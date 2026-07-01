using DASHBOARD.Application.Sprints.DTOs;
using MediatR;

namespace DASHBOARD.Application.Sprints.Queries.GetSprintDetail;

/// <summary>Returns the full sprint planning snapshot for one sprint.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintId">The sprint to load.</param>
public sealed record GetSprintDetailQuery(Guid RepositoryId, Guid SprintId)
    : IRequest<SprintDetailDto>;
