using DASHBOARD.Application.Capacity.DTOs;
using MediatR;

namespace DASHBOARD.Application.Capacity.Commands.AddDayOff;

/// <summary>Adds a day-off entry to a sprint. Date must fall within the sprint range. <paramref name="UserId"/> null means a team-wide day off.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintId">The sprint to add the day off to.</param>
/// <param name="UserId">The affected user, or <c>null</c> for a team-wide entry.</param>
/// <param name="Date">The date of the day off (must be within sprint range).</param>
/// <param name="Hours">Hours to deduct from capacity (typically 8 for a full day).</param>
/// <param name="Reason">Short reason description.</param>
public sealed record AddDayOffCommand(
    Guid     RepositoryId,
    Guid     SprintId,
    Guid?    UserId,
    DateOnly Date,
    decimal  Hours,
    string   Reason) : IRequest<DayOffDto>;
