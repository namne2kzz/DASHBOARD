using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Capacity.Commands.RemoveDayOff;

/// <summary>Removes a day-off entry from a sprint.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintId">The sprint.</param>
/// <param name="DayOffId">The day-off entry to remove.</param>
public sealed record RemoveDayOffCommand(Guid RepositoryId, Guid SprintId, Guid DayOffId) : IRequest<Result>;
