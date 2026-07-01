using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Repositories.Commands.DeleteMetadata;

/// <summary>Handles <see cref="DeleteMetadataCommand"/>: checks scope-based privilege (GlobalAdmin for global entries, ManageSettings for repo entries) then soft-deletes the catalog entry.</summary>
public sealed class DeleteMetadataCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<DeleteMetadataCommand, Result>
{
    /// <summary>Validates privilege, locates the entry, stamps soft-delete, and commits.</summary>
    /// <param name="command">The delete command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success.</returns>
    public async Task<Result> Handle(DeleteMetadataCommand command, CancellationToken ct)
    {
        var entry = await db.Set<RepositoryMetadata>()
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

        entry.DeletedByUserId = user.UserId;
        db.Set<RepositoryMetadata>().Remove(entry);

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
