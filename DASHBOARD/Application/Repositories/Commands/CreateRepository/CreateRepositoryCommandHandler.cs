using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Repositories.DTOs;
using DASHBOARD.Domain.Constants;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Repositories.Commands.CreateRepository;

/// <summary>Handles <see cref="CreateRepositoryCommand"/>: enforces global-admin gate, checks Code uniqueness, validates the lead, creates the repository and seeds them as its Project Manager.</summary>
public sealed class CreateRepositoryCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<CreateRepositoryCommand, RepositoryDto>
{
    /// <summary>Validates permissions, ensures Code uniqueness, verifies the Scrum Master exists, then persists the repository and membership.</summary>
    /// <param name="command">The create command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created <see cref="RepositoryDto"/>.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the caller is not a global admin.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the Code is already in use or the specified Scrum Master does not exist.</exception>
    public async Task<RepositoryDto> Handle(CreateRepositoryCommand command, CancellationToken ct)
    {
        if (!await user.IsGlobalAdminAsync(ct))
            throw new ForbiddenException("Only global admins may create repositories.");

        var orgId = user.OrgId;

        // Enforce the organization's license repo capacity.
        var org = await db.Set<Organization>().AsNoTracking().FirstOrDefaultAsync(o => o.Id == orgId, ct)
            ?? throw new InvalidOperationException("Organization not found.");
        var repoCount = await db.Set<Repository>().CountAsync(r => r.OrgId == orgId, ct);
        if (repoCount >= org.LicenseRepoCapacity)
            throw new InvalidOperationException(
                $"Your license allows up to {org.LicenseRepoCapacity} repositories. Contact Nexus to increase your capacity.");

        // Code is unique per organization.
        var codeUpper = command.Code.ToUpperInvariant();
        if (await db.Set<Repository>().AnyAsync(r => r.OrgId == orgId && r.Code == codeUpper, ct))
            throw new InvalidOperationException($"A repository with code '{codeUpper}' already exists.");

        // The lead must be a user in the same organization.
        var leadExists = await db.Set<User>().AnyAsync(u => u.Id == command.ProjectManagerId && u.OrgId == orgId, ct);
        if (!leadExists)
            throw new InvalidOperationException($"User '{command.ProjectManagerId}' does not exist.");

        var repo = new Repository
        {
            OrgId       = orgId,
            Name        = command.Name,
            Code        = codeUpper,
            Description = command.Description,
        };
        db.Set<Repository>().Add(repo);

        // Bootstrap the repository's own standard roles + discipline catalog (no shared/seeded ids).
        Role? projectManagerRole = null;
        foreach (var def in DefaultRoleDefinitions.All)
        {
            var role = new Role
            {
                RepositoryId     = repo.Id,
                IsDefault        = true,
                Name             = def.Name,
                Description      = def.Description,
                AllowedFunctions = [.. def.AllowedFunctions],
            };
            db.Set<Role>().Add(role);
            if (def.Name == DefaultRoleDefinitions.ProjectManager)
                projectManagerRole = role;

            db.Set<RepositoryMetadata>().Add(new RepositoryMetadata
            {
                RepositoryId = repo.Id,
                IsGlobal     = false,
                Key          = MetadataKey.RepoRole,
                Value        = def.Name,
            });
        }

        db.Set<RepositoryMember>().Add(new RepositoryMember
        {
            UserId       = command.ProjectManagerId,
            RepositoryId = repo.Id,
            DefaultRole  = DefaultRoleDefinitions.ProjectManager,
            RoleId       = projectManagerRole!.Id,
        });

        await uow.CommitAsync(ct);

        // The caller just named the lead, so the response can carry them straight back
        // rather than making the client re-fetch to learn what it already chose.
        var leadName = await db.Set<User>().AsNoTracking()
            .Where(u => u.Id == command.ProjectManagerId)
            .Select(u => u.Name)
            .FirstOrDefaultAsync(ct);

        return new RepositoryDto(
            repo.Id, repo.Name, repo.Code, repo.Description, 1, repo.CreatedAt,
            IsArchived:      false,
            LeadId:     command.ProjectManagerId,
            LeadName:   leadName);
    }
}
