using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Repositories.Commands.UpdateRepository;

/// <summary>Handles <see cref="UpdateRepositoryCommand"/>: checks <see cref="SystemFunction.EditRepository"/> permission then saves changes.</summary>
public sealed class UpdateRepositoryCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<UpdateRepositoryCommand, Result>
{
    /// <summary>Validates edit permission, applies the name and description change, and commits.</summary>
    /// <param name="command">The update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success; <see cref="Result.Failure"/> on permission denial.</returns>
    public async Task<Result> Handle(UpdateRepositoryCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.EditRepository, ct))
            throw new ForbiddenException("You do not have permission to edit this repository.");

        var repo = await db.Set<Repository>()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.Id == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(Repository), command.RepositoryId);

        repo.Name        = command.Name;
        repo.Description = command.Description;
        repo.Touch();

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
