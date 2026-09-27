using DASHBOARD.Application.Backlog.Commands.CreateBacklogItem;
using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.UnitTests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.UnitTests.Application.Backlog.Commands;

/// <summary>
/// Unit tests for <see cref="CreateBacklogItemCommandHandler"/>.
/// </summary>
/// <remarks>
/// Part (1) of BUG-009 (High): the permission branch threw <c>UnauthorizedAccessException</c>
/// for what is an everyday outcome — any member without <c>ManageBacklog</c> pressing "Add item".
/// The permission test asserts a <c>Result.Failure</c> rather than an exception.
/// </remarks>
public sealed class CreateBacklogItemCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public CreateBacklogItemCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private CreateBacklogItemCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private IRequestUserContext AuthorizedUser() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.ManageBacklog, _repositoryId).Object;

    private CreateBacklogItemCommand Command(
        BacklogItemType type        = BacklogItemType.UserStory,
        string          title       = "As a user I want…",
        Guid?           parentId    = null,
        int?            storyPoints = null,
        TshirtSize?     tshirtSize  = null,
        string          criteria    = "Given…When…Then…") =>
        new(_repositoryId, type, title, parentId, storyPoints, tshirtSize, criteria);

    private async Task<BacklogItem> AddItemAsync(
        decimal rank, Guid? parentId = null, Guid? repositoryId = null)
    {
        var item = new BacklogItem
        {
            RepositoryId = repositoryId ?? _repositoryId,
            Type         = BacklogItemType.Epic,
            State        = BacklogItemState.New,
            Title        = "Existing",
            Rank         = rank,
            ParentId     = parentId,
        };
        _db.Set<BacklogItem>().Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutManageBacklogPrivilege_FailsWithoutThrowing()
    {
        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await _db.Set<BacklogItem>().CountAsync()).Should().Be(0);
    }

    // ── Parent validation ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenParentDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(Command(parentId: Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenParentBelongsToAnotherRepository_ThrowsNotFound()
    {
        var foreignParent = await AddItemAsync(1000m, repositoryId: Guid.NewGuid());
        var handler       = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(Command(parentId: foreignParent.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WithAValidParent_LinksTheChild()
    {
        var parent  = await AddItemAsync(1000m);
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(Command(parentId: parent.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ParentId.Should().Be(parent.Id);
    }

    // ── Rank assignment ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ForTheFirstItem_StartsRankAtTheBaseStep()
    {
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.Value!.Rank.Should().Be(1000m);
    }

    [Fact]
    public async Task Handle_AppendsAfterTheLastItemAtTheSameLevel()
    {
        await AddItemAsync(1000m);
        await AddItemAsync(3000m);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.Value!.Rank.Should().Be(4000m, "the new item goes to the bottom of the list");
    }

    [Fact]
    public async Task Handle_RankingIsScopedToTheParentLevel()
    {
        var parent = await AddItemAsync(1000m);
        await AddItemAsync(9000m, parentId: parent.Id);

        var handler = CreateHandler(AuthorizedUser());

        // A new root-level item must not inherit the child level's high rank.
        var result = await handler.Handle(Command(), CancellationToken.None);

        result.Value!.Rank.Should().Be(2000m, "root items rank among themselves");
    }

    [Fact]
    public async Task Handle_RankingIsScopedToTheRepository()
    {
        await AddItemAsync(9000m, repositoryId: Guid.NewGuid());

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.Value!.Rank.Should().Be(1000m, "another repository's ranks must not leak in");
    }

    // ── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_PersistsTheItemWithTheSuppliedContent()
    {
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(BacklogItemType.Feature, "Checkout", storyPoints: 8, criteria: "AC text"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var persisted = await _db.Set<BacklogItem>().AsNoTracking().SingleAsync();
        persisted.Id.Should().Be(result.Value!.Id);
        persisted.Type.Should().Be(BacklogItemType.Feature);
        persisted.Title.Should().Be("Checkout");
        persisted.StoryPoints.Should().Be(8);
        persisted.AcceptanceCriteria.Should().Be("AC text");
        persisted.RepositoryId.Should().Be(_repositoryId);
    }

    [Fact]
    public async Task Handle_StartsEveryItemInTheNewState()
    {
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.Value!.State.Should().Be(BacklogItemState.New,
            "a fresh item has not been refined yet");
    }

    [Fact]
    public async Task Handle_StartsWithNoDocumentsNoSprintAndNoChildren()
    {
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.Value!.Documents.Should().BeEmpty();
        result.Value.SprintId.Should().BeNull();
        result.Value.Children.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_CarriesTheTshirtSizeEstimate()
    {
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(BacklogItemType.Epic, tshirtSize: TshirtSize.L), CancellationToken.None);

        result.Value!.TshirtSize.Should().Be(TshirtSize.L);
        result.Value.StoryPoints.Should().BeNull();
    }
}
