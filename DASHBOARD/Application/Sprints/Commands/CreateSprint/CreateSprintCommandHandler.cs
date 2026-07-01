using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Sprints.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Sprints.Commands.CreateSprint;

/// <summary>Handles <see cref="CreateSprintCommand"/>: checks ManageSprint permission then creates the sprint (not yet active).</summary>
public sealed class CreateSprintCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<CreateSprintCommand, SprintDto>
{
    /// <summary>Validates permission then creates the sprint with IsActive = false.</summary>
    /// <param name="command">The create command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created <see cref="SprintDto"/>.</returns>
    public async Task<SprintDto> Handle(CreateSprintCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageSprint, ct))
            throw new UnauthorizedAccessException("You do not have permission to manage sprints in this repository.");

        var overlaps = await db.Set<Sprint>()
            .AnyAsync(s => s.RepositoryId == command.RepositoryId
                        && s.StartDate    <= command.EndDate
                        && s.EndDate      >= command.StartDate, ct);

        if (overlaps)
            throw new ValidationException([new ValidationFailure("DateRange",
                "Sprint dates overlap with an existing sprint in this repository.")]);

        var sprint = new Sprint
        {
            RepositoryId = command.RepositoryId,
            Name         = command.Name,
            StartDate    = command.StartDate,
            EndDate      = command.EndDate,
        };
        db.Set<Sprint>().Add(sprint);
        await uow.CommitAsync(ct);

        var today    = DateOnly.FromDateTime(DateTime.UtcNow);
        var isActive = sprint.StartDate <= today && sprint.EndDate >= today;
        return new SprintDto(sprint.Id, sprint.RepositoryId, sprint.Name, sprint.StartDate, sprint.EndDate, isActive, sprint.CreatedAt);
    }
}
