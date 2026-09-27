using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Repositories.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Repositories.Commands.AddMetadata;

/// <summary>Handles <see cref="AddMetadataCommand"/>: checks ManageSettings privilege, prevents duplicate values for the same key, then adds the entry.</summary>
public sealed class AddMetadataCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<AddMetadataCommand, RepositoryMetadataDto>
{
    /// <summary>Validates privilege and uniqueness, creates the metadata entry, and returns the DTO.</summary>
    /// <param name="command">The add command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created <see cref="RepositoryMetadataDto"/>.</returns>
    public async Task<RepositoryMetadataDto> Handle(AddMetadataCommand command, CancellationToken ct)
    {
        // Global entries are admin-only; repository entries require ManageSettings on that repo.
        if (command.IsGlobal)
        {
            if (!await user.IsGlobalAdminAsync(ct))
                throw new ForbiddenException("Only global admins may manage global metadata.");
        }
        else if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageMetadata, ct))
        {
            throw new ForbiddenException("You do not have permission to manage settings in this repository.");
        }

        Guid? repositoryId = command.IsGlobal ? null : command.RepositoryId;

        var duplicate = await db.Set<RepositoryMetadata>()
            .AnyAsync(m => m.RepositoryId == repositoryId
                        && m.Key   == command.Key
                        && m.Value == command.Value, ct);

        if (duplicate)
            throw new InvalidOperationException($"Value '{command.Value}' already exists for key '{command.Key}'.");

        var entry = new RepositoryMetadata
        {
            RepositoryId = repositoryId,
            IsGlobal     = command.IsGlobal,
            Key          = command.Key,
            Value        = command.Value,
        };
        db.Set<RepositoryMetadata>().Add(entry);
        await uow.CommitAsync(ct);

        return new RepositoryMetadataDto(entry.Id, entry.RepositoryId, entry.IsGlobal, entry.Key.ToString(), entry.Key.GetDisplayName(), entry.Value, entry.CreatedAt);
    }
}
