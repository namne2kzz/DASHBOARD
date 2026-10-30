using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Repositories.Commands.UnarchiveRepository;

/// <summary>Handles <see cref="UnarchiveRepositoryCommand"/>: enforces the global-admin gate, clears the archived flag, and commits.</summary>
public sealed class UnarchiveRepositoryCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<UnarchiveRepositoryCommand, Result>
{
    /// <summary>Restores the repository by clearing its <c>IsArchived</c> flag. Only global admins may perform this.</summary>
    /// <param name="command">The restore command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success.</returns>
    /// <exception cref="ForbiddenException">Thrown when the caller is not a global admin.</exception>
    /// <exception cref="NotFoundException">Thrown when no repository has that id.</exception>
    public async Task<Result> Handle(UnarchiveRepositoryCommand command, CancellationToken ct)
    {
        if (!await user.IsGlobalAdminAsync(ct))
            throw new ForbiddenException("Only global admins may restore repositories.");

        // Scoped to the caller's organization: an id from another tenant must read as not found,
        // not as a repository somebody else gets to restore.
        var repo = await db.Set<Repository>()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.Id == command.RepositoryId && r.OrgId == user.OrgId, ct)
            ?? throw new NotFoundException(nameof(Repository), command.RepositoryId);

        repo.IsArchived = false;
        repo.Touch();

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
