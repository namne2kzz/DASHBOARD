using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Sprints.DTOs;
using MediatR;

namespace DASHBOARD.Application.Sprints.Commands.CreateSprint;

/// <summary>Creates a new sprint within a repository. Requires <see cref="DASHBOARD.Domain.Enums.SystemFunction.ManageSprint"/>.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="Name">Sprint display name (e.g. "Sprint 1 — May 2026").</param>
/// <param name="StartDate">Inclusive start date.</param>
/// <param name="EndDate">Inclusive end date.</param>
public sealed record CreateSprintCommand(
    Guid     RepositoryId,
    string   Name,
    DateOnly StartDate,
    DateOnly EndDate) : IRequest<Result<SprintDto>>;
