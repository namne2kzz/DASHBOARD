using DASHBOARD.Application.Backlog.Commands.DeleteBacklogItem;
using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.UnitTests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.UnitTests.Application.Backlog.Commands;

/// <summary>Unit tests for <see cref="DeleteBacklogItemCommandHandler"/>.</summary>
public sealed class DeleteBacklogItemCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public DeleteBacklogItemCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private DeleteBacklogItemCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private IRequestUserContext AuthorizedUser() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.ManageBacklog, _repositoryId).Object;

    private async Task<BacklogItem> AddItemAsync(
        string title = "Item", Guid? parentId = null, Guid? repositoryId = null)
    {
        var item = new BacklogItem
        {
            RepositoryId = repositoryId ?? _repositoryId,
            Type         = BacklogItemType.UserStory,
            State        = BacklogItemState.New,
            Title        = title,
            Rank         = 1000m,
            ParentId     = parentId,
        };
        _db.Set<BacklogItem>().Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutManageBacklogPrivilege_FailsAndKeepsItem()
    {
        var item    = await AddItemAsync();
        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(
            new DeleteBacklogItemCommand(_repositoryId, item.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await _db.Set<BacklogItem>().CountAsync()).Should().Be(1);
    }

    // ── Not found ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenItemDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new DeleteBacklogItemCommand(_repositoryId, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenItemBelongsToAnotherRepository_ThrowsNotFound()
    {
        var foreign = await AddItemAsync(repositoryId: Guid.NewGuid());
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new DeleteBacklogItemCommand(_repositoryId, foreign.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Children guard ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheItemHasChildren_Fails()
    {
        var parent = await AddItemAsync("Parent");
        await AddItemAsync("Child", parentId: parent.Id);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new DeleteBacklogItemCommand(_repositoryId, parent.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("has children");
        (await _db.Set<BacklogItem>().CountAsync()).Should().Be(2, "nothing may be deleted");
    }

    [Fact]
    public async Task Handle_AfterItsChildrenAreGone_TheParentCanBeDeleted()
    {
        var parent = await AddItemAsync("Parent");
        var child  = await AddItemAsync("Child", parentId: parent.Id);

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(new DeleteBacklogItemCommand(_repositoryId, child.Id), CancellationToken.None);
        var result = await handler.Handle(
            new DeleteBacklogItemCommand(_repositoryId, parent.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _db.Set<BacklogItem>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_DeletingALeafUnderAParentIsAllowed()
    {
        var parent = await AddItemAsync("Parent");
        var child  = await AddItemAsync("Child", parentId: parent.Id);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new DeleteBacklogItemCommand(_repositoryId, child.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _db.Set<BacklogItem>().AnyAsync(b => b.Id == parent.Id)).Should().BeTrue();
    }

    // ── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_DeletesAChildlessItem()
    {
        var item    = await AddItemAsync();
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new DeleteBacklogItemCommand(_repositoryId, item.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _db.Set<BacklogItem>().AnyAsync(b => b.Id == item.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_LeavesUnrelatedItemsAlone()
    {
        var target    = await AddItemAsync("Target");
        var bystander = await AddItemAsync("Bystander");

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(new DeleteBacklogItemCommand(_repositoryId, target.Id), CancellationToken.None);

        (await _db.Set<BacklogItem>().AnyAsync(b => b.Id == bystander.Id)).Should().BeTrue();
    }
}
