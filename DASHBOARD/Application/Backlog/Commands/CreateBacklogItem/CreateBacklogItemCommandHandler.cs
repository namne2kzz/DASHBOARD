using DASHBOARD.Application.Backlog.DTOs;
using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Backlog.Commands.CreateBacklogItem;

/// <summary>Handles <see cref="CreateBacklogItemCommand"/>: validates hierarchy rules, computes initial rank at end of list, and persists the item.</summary>
public sealed class CreateBacklogItemCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<CreateBacklogItemCommand, Result<BacklogItemDto>>
{
    /// <summary>Validates permission, enforces parent-child type rules, assigns rank, and creates the item.</summary>
    /// <param name="command">The create command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created <see cref="BacklogItemDto"/>.</returns>
    public async Task<Result<BacklogItemDto>> Handle(CreateBacklogItemCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.CreateWorkItem, ct))
            return Result<BacklogItemDto>.Failure("You do not have permission to create backlog items in this repository.");

        // Validate parent exists and belongs to the same repo.
        if (command.ParentId.HasValue &&
            !await db.Set<BacklogItem>().AnyAsync(b => b.Id == command.ParentId && b.RepositoryId == command.RepositoryId, ct))
            throw new NotFoundException(nameof(BacklogItem), command.ParentId.Value);

        // Rank: append to the bottom of the same parent level.
        var maxRank = await db.Set<BacklogItem>()
            .AsNoTracking()
            .Where(b => b.RepositoryId == command.RepositoryId && b.ParentId == command.ParentId)
            .MaxAsync(b => (decimal?)b.Rank, ct) ?? 0m;

        var item = new BacklogItem
        {
            RepositoryId       = command.RepositoryId,
            Type               = command.Type,
            Title              = command.Title,
            ParentId           = command.ParentId,
            Rank               = maxRank + 1000m,
            State              = BacklogItemState.New,
            StoryPoints        = command.StoryPoints,
            TshirtSize         = command.TshirtSize,
            AcceptanceCriteria = command.AcceptanceCriteria,
            Documents          = [],
        };
        db.Set<BacklogItem>().Add(item);
        await uow.CommitAsync(ct);

        return Result<BacklogItemDto>.Success(ToDto(item, []));
    }

    internal static BacklogItemDto ToDto(BacklogItem b, IReadOnlyList<BacklogItemDto> children) =>
        new(b.Id, b.RepositoryId, b.Type, b.Title, b.ParentId, b.Rank, b.SprintId, b.Sprint?.Name,
            b.State, b.StoryPoints, b.TshirtSize, b.AcceptanceCriteria, b.Documents, b.CreatedAt, children);
}
