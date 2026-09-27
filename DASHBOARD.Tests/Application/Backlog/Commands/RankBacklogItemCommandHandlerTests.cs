using DASHBOARD.Application.Backlog.Commands.RankBacklogItem;
using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Tests.Application.Backlog.Commands;

/// <summary>
/// Unit tests for <see cref="RankBacklogItemCommandHandler"/> — fractional ranking and re-normalisation.
/// </summary>
public sealed class RankBacklogItemCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public RankBacklogItemCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private RankBacklogItemCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private IRequestUserContext AuthorizedUser() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.ManageBacklog, _repositoryId).Object;

    private async Task<BacklogItem> AddItemAsync(
        string title, decimal rank, Guid? parentId = null, Guid? repositoryId = null)
    {
        var item = new BacklogItem
        {
            RepositoryId = repositoryId ?? _repositoryId,
            Type         = BacklogItemType.UserStory,
            State        = BacklogItemState.New,
            Title        = title,
            Rank         = rank,
            ParentId     = parentId,
        };
        _db.Set<BacklogItem>().Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    private async Task<decimal> RankOfAsync(Guid id) =>
        (await _db.Set<BacklogItem>().AsNoTracking().SingleAsync(b => b.Id == id)).Rank;

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutManageBacklogPrivilege_FailsAndLeavesRankUnchanged()
    {
        var item    = await AddItemAsync("Item", 1000m);
        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var result = await handler.Handle(
            new RankBacklogItemCommand(_repositoryId, item.Id, null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("permission");
        (await RankOfAsync(item.Id)).Should().Be(1000m);
    }

    // ── Not found ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenItemDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new RankBacklogItemCommand(_repositoryId, Guid.NewGuid(), null, null), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenTheNeighbourDoesNotExist_ThrowsNotFound()
    {
        var item    = await AddItemAsync("Item", 1000m);
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new RankBacklogItemCommand(_repositoryId, item.Id, Guid.NewGuid(), null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenTheNeighbourBelongsToAnotherRepository_ThrowsNotFound()
    {
        var item    = await AddItemAsync("Item", 1000m);
        var foreign = await AddItemAsync("Foreign", 2000m, repositoryId: Guid.NewGuid());
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new RankBacklogItemCommand(_repositoryId, item.Id, foreign.Id, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Rank placement ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_MovingToAnEmptyListUsesTheBaseRank()
    {
        var item    = await AddItemAsync("Only", 5m);
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new RankBacklogItemCommand(_repositoryId, item.Id, null, null), CancellationToken.None);

        (await RankOfAsync(item.Id)).Should().Be(1000m);
    }

    [Fact]
    public async Task Handle_MovingToTheTopHalvesTheFirstRank()
    {
        var first   = await AddItemAsync("First", 1000m);
        var moving  = await AddItemAsync("Moving", 3000m);
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new RankBacklogItemCommand(_repositoryId, moving.Id, null, first.Id), CancellationToken.None);

        (await RankOfAsync(moving.Id)).Should().Be(500m);
    }

    [Fact]
    public async Task Handle_MovingToTheBottomAddsAFullStep()
    {
        var last    = await AddItemAsync("Last", 3000m);
        var moving  = await AddItemAsync("Moving", 1000m);
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new RankBacklogItemCommand(_repositoryId, moving.Id, last.Id, null), CancellationToken.None);

        (await RankOfAsync(moving.Id)).Should().Be(4000m);
    }

    [Fact]
    public async Task Handle_MovingBetweenTwoItemsTakesTheMidpoint()
    {
        var prev    = await AddItemAsync("Prev", 1000m);
        var next    = await AddItemAsync("Next", 2000m);
        var moving  = await AddItemAsync("Moving", 5000m);
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new RankBacklogItemCommand(_repositoryId, moving.Id, prev.Id, next.Id),
            CancellationToken.None);

        (await RankOfAsync(moving.Id)).Should().Be(1500m);
    }

    [Fact]
    public async Task Handle_RepeatedMidpointMovesKeepShrinkingTheGap()
    {
        var prev    = await AddItemAsync("Prev", 1000m);
        var next    = await AddItemAsync("Next", 1002m);
        var moving  = await AddItemAsync("Moving", 5000m);
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new RankBacklogItemCommand(_repositoryId, moving.Id, prev.Id, next.Id),
            CancellationToken.None);

        (await RankOfAsync(moving.Id)).Should().Be(1001m, "the gap of 2 is still far above the threshold");
    }

    [Fact]
    public async Task Handle_StampsTheUpdatedTimestamp()
    {
        var item    = await AddItemAsync("Item", 1000m);
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new RankBacklogItemCommand(_repositoryId, item.Id, null, null), CancellationToken.None);

        var updated = await _db.Set<BacklogItem>().AsNoTracking().SingleAsync(b => b.Id == item.Id);
        updated.UpdatedAt.Should().NotBeNull();
    }

    // ── Re-normalisation when precision runs out ────────────────────────────

    [Fact]
    public async Task Handle_WhenTheGapCollapses_RenormalisesSiblingsToWholeSteps()
    {
        // Neighbours are 0.0005 apart — below the 0.001 minimum gap.
        var prev    = await AddItemAsync("Prev", 1000.0000m);
        var next    = await AddItemAsync("Next", 1000.0005m);
        var moving  = await AddItemAsync("Moving", 5000m);
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new RankBacklogItemCommand(_repositoryId, moving.Id, prev.Id, next.Id),
            CancellationToken.None);

        (await RankOfAsync(prev.Id)).Should().Be(1000m);
        (await RankOfAsync(next.Id)).Should().Be(2000m);
        (await RankOfAsync(moving.Id)).Should().Be(3000m,
            "the moved item is appended after the re-normalised siblings");
    }

    [Fact]
    public async Task Handle_RenormalisationPreservesTheExistingOrder()
    {
        var third   = await AddItemAsync("Third",  1000.0010m);
        var first   = await AddItemAsync("First",  1000.0000m);
        var second  = await AddItemAsync("Second", 1000.0005m);
        var moving  = await AddItemAsync("Moving", 5000m);
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new RankBacklogItemCommand(_repositoryId, moving.Id, first.Id, second.Id),
            CancellationToken.None);

        var ranks = await _db.Set<BacklogItem>().AsNoTracking()
            .Where(b => b.Id != moving.Id)
            .OrderBy(b => b.Rank)
            .Select(b => b.Title)
            .ToListAsync();

        ranks.Should().ContainInOrder("First", "Second", "Third");
    }

    [Fact]
    public async Task Handle_RenormalisationIsScopedToTheSameParent()
    {
        var parentId = Guid.NewGuid();
        var prev     = await AddItemAsync("Prev", 1000.0000m);
        var next     = await AddItemAsync("Next", 1000.0005m);
        var moving   = await AddItemAsync("Moving", 5000m);
        var otherBranch = await AddItemAsync("OtherBranch", 7777m, parentId: parentId);

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new RankBacklogItemCommand(_repositoryId, moving.Id, prev.Id, next.Id),
            CancellationToken.None);

        (await RankOfAsync(otherBranch.Id)).Should().Be(7777m,
            "items under a different parent are a separate sibling group");
    }

    [Fact]
    public async Task Handle_RenormalisationIsScopedToTheSameRepository()
    {
        var prev    = await AddItemAsync("Prev", 1000.0000m);
        var next    = await AddItemAsync("Next", 1000.0005m);
        var moving  = await AddItemAsync("Moving", 5000m);
        var foreign = await AddItemAsync("Foreign", 8888m, repositoryId: Guid.NewGuid());

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new RankBacklogItemCommand(_repositoryId, moving.Id, prev.Id, next.Id),
            CancellationToken.None);

        (await RankOfAsync(foreign.Id)).Should().Be(8888m);
    }

    [Fact]
    public async Task Handle_WhenTheGapIsExactlyTheThreshold_DoesNotRenormalise()
    {
        // The guard is a strict "<", so a gap of exactly 0.001 still takes the midpoint.
        var prev    = await AddItemAsync("Prev", 1000.000m);
        var next    = await AddItemAsync("Next", 1000.001m);
        var moving  = await AddItemAsync("Moving", 5000m);
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new RankBacklogItemCommand(_repositoryId, moving.Id, prev.Id, next.Id),
            CancellationToken.None);

        (await RankOfAsync(prev.Id)).Should().Be(1000.000m, "no re-normalisation should have run");
        (await RankOfAsync(moving.Id)).Should().Be(1000.0005m);
    }
}
