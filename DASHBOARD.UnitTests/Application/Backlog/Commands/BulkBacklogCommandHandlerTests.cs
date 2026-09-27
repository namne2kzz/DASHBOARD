using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Backlog.Commands.BulkDeleteBacklogItems;
using DASHBOARD.Application.Backlog.Commands.BulkUpdateBacklogState;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.UnitTests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.UnitTests.Application.Backlog.Commands;

/// <summary>Unit tests for the bulk backlog operations.</summary>
public sealed class BulkBacklogCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public BulkBacklogCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private BulkDeleteBacklogItemsCommandHandler DeleteHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private BulkUpdateBacklogStateCommandHandler StateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private IRequestUserContext AuthorizedUser() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.ManageBacklog, _repositoryId).Object;

    private async Task<BacklogItem> AddItemAsync(
        string           title        = "Item",
        BacklogItemState state        = BacklogItemState.New,
        Guid?            parentId     = null,
        Guid?            repositoryId = null)
    {
        var item = new BacklogItem
        {
            RepositoryId = repositoryId ?? _repositoryId,
            Type         = BacklogItemType.UserStory,
            State        = state,
            Title        = title,
            Rank         = 1000m,
            ParentId     = parentId,
        };
        _db.Set<BacklogItem>().Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    // ── Bulk delete: permission and empty input ─────────────────────────────

    [Fact]
    public async Task BulkDelete_WithoutManageBacklogPrivilege_Fails()
    {
        var item    = await AddItemAsync();
        var handler = DeleteHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(
            new BulkDeleteBacklogItemsCommand(_repositoryId, [item.Id]), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await _db.Set<BacklogItem>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task BulkDelete_WithNoIds_SucceedsWithAnEmptySummary()
    {
        var handler = DeleteHandler(AuthorizedUser());

        var result = await handler.Handle(
            new BulkDeleteBacklogItemsCommand(_repositoryId, []), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Affected.Should().Be(0);
        result.Value.Skipped.Should().Be(0);
    }

    // ── Bulk delete: orphan protection ──────────────────────────────────────

    [Fact]
    public async Task BulkDelete_SkipsAParentWhoseChildIsNotSelected()
    {
        var parent = await AddItemAsync("Parent");
        await AddItemAsync("Child", parentId: parent.Id);

        var handler = DeleteHandler(AuthorizedUser());

        var result = await handler.Handle(
            new BulkDeleteBacklogItemsCommand(_repositoryId, [parent.Id]), CancellationToken.None);

        result.Value!.Affected.Should().Be(0);
        result.Value.Skipped.Should().Be(1);
        (await _db.Set<BacklogItem>().CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task BulkDelete_DeletesAParentWhenItsChildIsSelectedToo()
    {
        var parent = await AddItemAsync("Parent");
        var child  = await AddItemAsync("Child", parentId: parent.Id);

        var handler = DeleteHandler(AuthorizedUser());

        var result = await handler.Handle(
            new BulkDeleteBacklogItemsCommand(_repositoryId, [parent.Id, child.Id]),
            CancellationToken.None);

        result.Value!.Affected.Should().Be(2, "selecting the whole subtree orphans nobody");
        result.Value.Skipped.Should().Be(0);
        (await _db.Set<BacklogItem>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task BulkDelete_SkipsOnlyTheBlockedItemsAndDeletesTheRest()
    {
        var parent     = await AddItemAsync("Parent");
        await AddItemAsync("Child", parentId: parent.Id);
        var standalone = await AddItemAsync("Standalone");

        var handler = DeleteHandler(AuthorizedUser());

        var result = await handler.Handle(
            new BulkDeleteBacklogItemsCommand(_repositoryId, [parent.Id, standalone.Id]),
            CancellationToken.None);

        result.Value!.Affected.Should().Be(1);
        result.Value.Skipped.Should().Be(1);
        (await _db.Set<BacklogItem>().AnyAsync(b => b.Id == standalone.Id)).Should().BeFalse();
        (await _db.Set<BacklogItem>().AnyAsync(b => b.Id == parent.Id)).Should().BeTrue();
    }

    // ── Bulk delete: scoping and input hygiene ──────────────────────────────

    [Fact]
    public async Task BulkDelete_IgnoresItemsFromAnotherRepository()
    {
        var mine    = await AddItemAsync("Mine");
        var foreign = await AddItemAsync("Foreign", repositoryId: Guid.NewGuid());

        var handler = DeleteHandler(AuthorizedUser());

        var result = await handler.Handle(
            new BulkDeleteBacklogItemsCommand(_repositoryId, [mine.Id, foreign.Id]),
            CancellationToken.None);

        result.Value!.Affected.Should().Be(1);
        result.Value.Skipped.Should().Be(1, "the out-of-repository id is reported back, not silently dropped");
        (await _db.Set<BacklogItem>().AnyAsync(b => b.Id == foreign.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task BulkDelete_CountsADuplicatedIdOnlyOnce()
    {
        var item    = await AddItemAsync();
        var handler = DeleteHandler(AuthorizedUser());

        var result = await handler.Handle(
            new BulkDeleteBacklogItemsCommand(_repositoryId, [item.Id, item.Id]),
            CancellationToken.None);

        result.Value!.Affected.Should().Be(1);
    }

    [Fact]
    public async Task BulkDelete_CountsUnknownIdsAsSkipped()
    {
        var item    = await AddItemAsync();
        var handler = DeleteHandler(AuthorizedUser());

        var result = await handler.Handle(
            new BulkDeleteBacklogItemsCommand(_repositoryId, [item.Id, Guid.NewGuid()]),
            CancellationToken.None);

        result.Value!.Affected.Should().Be(1);
        result.Value.Skipped.Should().Be(1,
            "Affected + Skipped must add up to the selection, matching BulkUpdateState");
    }

    // ── Bulk state: permission and target-state rule ────────────────────────

    [Fact]
    public async Task BulkUpdateState_WithoutManageBacklogPrivilege_Fails()
    {
        var item    = await AddItemAsync();
        var handler = StateHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(
            new BulkUpdateBacklogStateCommand(_repositoryId, [item.Id], BacklogItemState.Ready),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task BulkUpdateState_RejectsCommittedAsATargetState()
    {
        var item    = await AddItemAsync();
        var handler = StateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new BulkUpdateBacklogStateCommand(_repositoryId, [item.Id], BacklogItemState.Committed),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Promote to Sprint");

        var unchanged = await _db.Set<BacklogItem>().AsNoTracking().SingleAsync(b => b.Id == item.Id);
        unchanged.State.Should().Be(BacklogItemState.New);
    }

    [Fact]
    public async Task BulkUpdateState_WithNoIds_SucceedsWithAnEmptySummary()
    {
        var handler = StateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new BulkUpdateBacklogStateCommand(_repositoryId, [], BacklogItemState.Ready),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Affected.Should().Be(0);
    }

    // ── Bulk state: transitions ─────────────────────────────────────────────

    [Theory]
    [InlineData(BacklogItemState.New)]
    [InlineData(BacklogItemState.Refining)]
    [InlineData(BacklogItemState.Ready)]
    public async Task BulkUpdateState_AppliesTheTargetStateToEverySelectedItem(BacklogItemState target)
    {
        var first  = await AddItemAsync("First");
        var second = await AddItemAsync("Second", state: BacklogItemState.Refining);

        var handler = StateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new BulkUpdateBacklogStateCommand(_repositoryId, [first.Id, second.Id], target),
            CancellationToken.None);

        result.Value!.Affected.Should().Be(2);

        var states = await _db.Set<BacklogItem>().AsNoTracking().Select(b => b.State).ToListAsync();
        states.Should().AllBeEquivalentTo(target);
    }

    [Fact]
    public async Task BulkUpdateState_SkipsItemsAlreadyCommittedToASprint()
    {
        var open      = await AddItemAsync("Open");
        var committed = await AddItemAsync("Committed", state: BacklogItemState.Committed);

        var handler = StateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new BulkUpdateBacklogStateCommand(_repositoryId, [open.Id, committed.Id],
                BacklogItemState.Ready),
            CancellationToken.None);

        result.Value!.Affected.Should().Be(1);
        result.Value.Skipped.Should().Be(1);

        var untouched = await _db.Set<BacklogItem>().AsNoTracking().SingleAsync(b => b.Id == committed.Id);
        untouched.State.Should().Be(BacklogItemState.Committed,
            "a promoted item must not be silently reverted");
    }

    [Fact]
    public async Task BulkUpdateState_StampsTheUpdatedTimestamp()
    {
        var item    = await AddItemAsync();
        var handler = StateHandler(AuthorizedUser());

        await handler.Handle(
            new BulkUpdateBacklogStateCommand(_repositoryId, [item.Id], BacklogItemState.Ready),
            CancellationToken.None);

        var updated = await _db.Set<BacklogItem>().AsNoTracking().SingleAsync(b => b.Id == item.Id);
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task BulkUpdateState_IgnoresItemsFromAnotherRepository()
    {
        var mine    = await AddItemAsync("Mine");
        var foreign = await AddItemAsync("Foreign", repositoryId: Guid.NewGuid());

        var handler = StateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new BulkUpdateBacklogStateCommand(_repositoryId, [mine.Id, foreign.Id],
                BacklogItemState.Ready),
            CancellationToken.None);

        result.Value!.Affected.Should().Be(1);

        var untouched = await _db.Set<BacklogItem>().AsNoTracking().SingleAsync(b => b.Id == foreign.Id);
        untouched.State.Should().Be(BacklogItemState.New);
    }

    [Fact]
    public async Task BulkUpdateState_CountsUnknownIdsAsSkipped()
    {
        // Unlike the delete summary, this one measures against the requested ids, so an id that
        // matches no row still shows up as skipped.
        var item    = await AddItemAsync();
        var handler = StateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new BulkUpdateBacklogStateCommand(_repositoryId, [item.Id, Guid.NewGuid()],
                BacklogItemState.Ready),
            CancellationToken.None);

        result.Value!.Affected.Should().Be(1);
        result.Value.Skipped.Should().Be(1);
    }
}
