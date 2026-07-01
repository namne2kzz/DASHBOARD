using DASHBOARD.Application.Capacity.DTOs;
using MediatR;

namespace DASHBOARD.Application.Capacity.Queries.GetCapacity;

/// <summary>Returns all capacity members and day-off entries for a sprint.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintId">The sprint to query.</param>
public sealed record GetCapacityQuery(Guid RepositoryId, Guid SprintId) : IRequest<GetCapacityResult>;

/// <summary>Combined capacity data for a sprint.</summary>
public sealed record GetCapacityResult(
    IReadOnlyList<CapacityMemberDto> Members,
    IReadOnlyList<DayOffDto>         DaysOff);
