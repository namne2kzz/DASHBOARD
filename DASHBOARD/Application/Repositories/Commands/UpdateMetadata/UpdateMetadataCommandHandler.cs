using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Repositories.Commands.UpdateMetadata;

/// <summary>Handles <see cref="UpdateMetadataCommand"/>: checks scope-based privilege, prevents duplicate values, then updates the entry value.</summary>
public sealed class UpdateMetadataCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<UpdateMetadataCommand, Result>
{
    /// <summary>Validates privilege and uniqueness, applies the new value, and commits.</summary>
    /// <param name="command">The update command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success.</returns>
    public async Task<Result> Handle(UpdateMetadataCommand command, CancellationToken ct)
    {
        var entry = await db.Set<RepositoryMetadata>()
            .AsTracking()
            .FirstOrDefaultAsync(m => m.Id == command.MetadataId, ct)
            ?? throw new NotFoundException(nameof(RepositoryMetadata), command.MetadataId);

        if (entry.IsGlobal)
        {
            if (!await user.IsGlobalAdminAsync(ct))
                return Result.Failure("Only global admins may manage global metadata.");
        }
        else if (!await user.CanAsync(entry.RepositoryId!.Value, SystemFunction.ManageSettings, ct))
        {
            return Result.Failure("You do not have permission to manage settings in this repository.");
        }

        var duplicate = await db.Set<RepositoryMetadata>()
            .AnyAsync(m => m.Id != entry.Id
                        && m.RepositoryId == entry.RepositoryId
                        && m.Key   == entry.Key
                        && m.Value == command.Value, ct);
        if (duplicate)
            return Result.Failure($"Value '{command.Value}' already exists for key '{entry.Key}'.");

        entry.Value = command.Value;
        entry.Touch();

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
