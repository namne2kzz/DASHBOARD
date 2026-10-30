using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Constants;
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
            // Scoped to the caller's organization: an id from another tenant must read as not
            // found, never as a repository somebody else gets to edit.
            .FirstOrDefaultAsync(r => r.Id == command.RepositoryId && r.OrgId == user.OrgId, ct)
            ?? throw new NotFoundException(nameof(Repository), command.RepositoryId);

        repo.Name        = command.Name;
        repo.Description = command.Description;
        repo.Touch();

        if (command.LeadId is { } newLeadId)
            await HandOverLeadAsync(repo, newLeadId, ct);

        await uow.CommitAsync(ct);
        return Result.Ok;
    }

    /// <summary>
    /// Moves the Project Manager role to another member, demoting the previous holder to Developer.
    /// </summary>
    /// <remarks>
    /// The role lives on the membership, not on the repository, so handing it over means editing
    /// two memberships. The outgoing holder keeps their place in the repository: removing them
    /// would orphan whatever is assigned to them, and a handover is not a removal.
    /// </remarks>
    /// <param name="repo">The repository being updated.</param>
    /// <param name="newLeadId">The member taking the role.</param>
    /// <param name="ct">Cancellation token.</param>
    private async Task HandOverLeadAsync(Repository repo, Guid newLeadId, CancellationToken ct)
    {
        var members = await db.Set<RepositoryMember>()
            .AsTracking()
            .Where(m => m.RepositoryId == repo.Id)
            .ToListAsync(ct);

        var incoming = members.FirstOrDefault(m => m.UserId == newLeadId)
            ?? throw new InvalidOperationException(
                $"User '{newLeadId}' is not a member of this repository.");

        if (incoming.DefaultRole == DefaultRoleDefinitions.ProjectManager)
            return; // Already holds it — nothing to hand over.

        var roles = await db.Set<Role>()
            .AsNoTracking()
            .Where(r => r.RepositoryId == repo.Id && r.IsDefault)
            .ToListAsync(ct);

        var leadRole = roles.FirstOrDefault(r => r.Name == DefaultRoleDefinitions.ProjectManager)
            ?? throw new InvalidOperationException("This repository has no Project Manager role defined.");
        var developerRole = roles.FirstOrDefault(r => r.Name == DefaultRoleDefinitions.Developer)
            ?? throw new InvalidOperationException("This repository has no Developer role defined.");

        // Both sides move in one transaction, which is what keeps the repository from ever being
        // without a Project Manager — see RepositoryLeadGuard. Splitting this into "demote, then
        // promote" would open exactly the gap that guard exists to prevent.
        foreach (var outgoing in members.Where(m =>
                     m.DefaultRole == DefaultRoleDefinitions.ProjectManager && m.UserId != newLeadId))
        {
            outgoing.DefaultRole = DefaultRoleDefinitions.Developer;
            outgoing.RoleId      = developerRole.Id;
            outgoing.Touch();
        }

        incoming.DefaultRole = DefaultRoleDefinitions.ProjectManager;
        incoming.RoleId      = leadRole.Id;
        incoming.Touch();
    }
}
