using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Repositories.Commands.ArchiveRepository;

/// <summary>Handles <see cref="ArchiveRepositoryCommand"/>: enforces global-admin gate, marks the repository as archived, and commits.</summary>
public sealed class ArchiveRepositoryCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<ArchiveRepositoryCommand, Result>
{
    /// <summary>Archives the repository by setting its <c>IsArchived</c> flag. Only global admins may perform this.</summary>
    /// <param name="command">The archive command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success; <see cref="Result.Failure"/> on permission denial.</returns>
    public async Task<Result> Handle(ArchiveRepositoryCommand command, CancellationToken ct)
    {
        if (!await user.IsGlobalAdminAsync(ct))
            throw new ForbiddenException("Only global admins may archive repositories.");

        var repo = await db.Set<Repository>()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.Id == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(Repository), command.RepositoryId);

        repo.IsArchived = true;
        repo.Touch();

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
