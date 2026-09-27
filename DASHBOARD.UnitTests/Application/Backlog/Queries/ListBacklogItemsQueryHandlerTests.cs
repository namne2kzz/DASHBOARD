using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Backlog.DTOs;
using DASHBOARD.Application.Backlog.Queries.ListBacklogItems;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.UnitTests.Common;

namespace DASHBOARD.UnitTests.Application.Backlog.Queries;

/// <summary>Unit tests for <see cref="ListBacklogItemsQueryHandler"/> — tree building and filtering.</summary>
public sealed class ListBacklogItemsQueryHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public ListBacklogItemsQueryHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private ListBacklogItemsQueryHandler CreateHandler(IRequestUserContext user) => new(_db, user);

    private IRequestUserContext MemberUser() =>
        RequestUserContextMock.ForUser().AsMember(_repositoryId).Object;

    private async Task<BacklogItem> AddItemAsync(
        string           title,
        decimal          rank,
        BacklogItemType  type         = BacklogItemType.UserStory,
        BacklogItemState state        = BacklogItemState.New,
        Guid?            parentId     = null,
        Guid?            repositoryId = null)
    {
        var item = new BacklogItem
        {
            RepositoryId = repositoryId ?? _repositoryId,
            Type         = type,
            State        = state,
            Title        = title,
            Rank         = rank,
            ParentId     = parentId,
        };
        _db.Set<BacklogItem>().Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    private static IEnumerable<BacklogItemDto> Flatten(IReadOnlyList<BacklogItemDto> items)
    {
        foreach (var item in items)
        {
            yield return item;
            foreach (var child in Flatten(item.Children))
                yield return child;
        }
    }

    // ── Access control ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserIsNotAMember_Throws()
    {
        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(new ListBacklogItemsQuery(_repositoryId), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    // ── Scoping ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithNoItems_ReturnsEmptyList()
    {
        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(new ListBacklogItemsQuery(_repositoryId), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyItemsOfTheRequestedRepository()
    {
        await AddItemAsync("Mine", 1000m);
        await AddItemAsync("Foreign", 2000m, repositoryId: Guid.NewGuid());

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(new ListBacklogItemsQuery(_repositoryId), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Title.Should().Be("Mine");
    }

    // ── Tree building ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsOnlyRootItemsAtTheTopLevel()
    {
        var epic = await AddItemAsync("Epic", 1000m, BacklogItemType.Epic);
        await AddItemAsync("Child", 2000m, parentId: epic.Id);

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(new ListBacklogItemsQuery(_repositoryId), CancellationToken.None);

        result.Should().ContainSingle("children are nested, not repeated at the root");
        result[0].Title.Should().Be("Epic");
    }

    [Fact]
    public async Task Handle_NestsChildrenUnderTheirParent()
    {
        var epic = await AddItemAsync("Epic", 1000m, BacklogItemType.Epic);
        await AddItemAsync("Story A", 1000m, parentId: epic.Id);
        await AddItemAsync("Story B", 2000m, parentId: epic.Id);

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(new ListBacklogItemsQuery(_repositoryId), CancellationToken.None);

        result[0].Children.Select(c => c.Title).Should().ContainInOrder("Story A", "Story B");
    }

    [Fact]
    public async Task Handle_BuildsTheTreeToArbitraryDepth()
    {
        var epic    = await AddItemAsync("Epic",    1000m, BacklogItemType.Epic);
        var feature = await AddItemAsync("Feature", 1000m, BacklogItemType.Feature, parentId: epic.Id);
        await AddItemAsync("Story", 1000m, parentId: feature.Id);

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(new ListBacklogItemsQuery(_repositoryId), CancellationToken.None);

        result[0].Children[0].Children.Should().ContainSingle()
              .Which.Title.Should().Be("Story");
    }

    // ── Ordering ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_OrdersByRankAscending()
    {
        await AddItemAsync("Third",  3000m);
        await AddItemAsync("First",  1000m);
        await AddItemAsync("Second", 2000m);

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(new ListBacklogItemsQuery(_repositoryId), CancellationToken.None);

        result.Select(r => r.Title).Should().ContainInOrder("First", "Second", "Third");
    }

    // ── Filters ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithStateFilter_ReturnsOnlyMatchingItems()
    {
        await AddItemAsync("Ready item", 1000m, state: BacklogItemState.Ready);
        await AddItemAsync("New item",   2000m, state: BacklogItemState.New);

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new ListBacklogItemsQuery(_repositoryId, StateFilter: BacklogItemState.Ready),
            CancellationToken.None);

        Flatten(result).Should().ContainSingle()
            .Which.Title.Should().Be("Ready item");
    }

    [Fact]
    public async Task Handle_WithTypeFilter_ReturnsOnlyMatchingItems()
    {
        await AddItemAsync("An epic", 1000m, BacklogItemType.Epic);
        await AddItemAsync("A story", 2000m, BacklogItemType.UserStory);

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new ListBacklogItemsQuery(_repositoryId, TypeFilter: BacklogItemType.Epic),
            CancellationToken.None);

        Flatten(result).Should().ContainSingle()
            .Which.Title.Should().Be("An epic");
    }

    [Fact]
    public async Task Handle_WithBothFilters_AppliesThemTogether()
    {
        await AddItemAsync("Ready epic",  1000m, BacklogItemType.Epic,      BacklogItemState.Ready);
        await AddItemAsync("New epic",    2000m, BacklogItemType.Epic,      BacklogItemState.New);
        await AddItemAsync("Ready story", 3000m, BacklogItemType.UserStory, BacklogItemState.Ready);

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new ListBacklogItemsQuery(_repositoryId, BacklogItemType.Epic, BacklogItemState.Ready),
            CancellationToken.None);

        Flatten(result).Should().ContainSingle()
            .Which.Title.Should().Be("Ready epic");
    }

    [Fact]
    public async Task Handle_WhenAMatchHasAnUnmatchedParent_KeepsTheParentForContext()
    {
        var epic = await AddItemAsync("Epic", 1000m, BacklogItemType.Epic);
        await AddItemAsync("Story under epic", 1000m, BacklogItemType.UserStory, parentId: epic.Id);

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new ListBacklogItemsQuery(_repositoryId, TypeFilter: BacklogItemType.UserStory),
            CancellationToken.None);

        // A User Story is never presented orphaned, so its Epic is kept even though the filter
        // excludes Epics.
        result.Should().ContainSingle().Which.Title.Should().Be("Epic");
        result[0].Children.Should().ContainSingle()
              .Which.Title.Should().Be("Story under epic");
    }

    [Fact]
    public async Task Handle_KeepsTheWholeAncestorChainOfAMatch()
    {
        var epic    = await AddItemAsync("Epic",    1000m, BacklogItemType.Epic);
        var feature = await AddItemAsync("Feature", 1000m, BacklogItemType.Feature, parentId: epic.Id);
        await AddItemAsync("Story", 1000m, BacklogItemType.UserStory, parentId: feature.Id);

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new ListBacklogItemsQuery(_repositoryId, TypeFilter: BacklogItemType.UserStory),
            CancellationToken.None);

        result.Should().ContainSingle().Which.Title.Should().Be("Epic");
        result[0].Children.Should().ContainSingle().Which.Title.Should().Be("Feature");
        result[0].Children[0].Children.Should().ContainSingle().Which.Title.Should().Be("Story");
    }

    [Fact]
    public async Task Handle_DropsAnAncestorThatLeadsToNoMatch()
    {
        var matchedEpic = await AddItemAsync("Matched epic", 1000m, BacklogItemType.Epic);
        await AddItemAsync("Story", 1000m, BacklogItemType.UserStory, parentId: matchedEpic.Id);
        await AddItemAsync("Barren epic", 2000m, BacklogItemType.Epic);

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new ListBacklogItemsQuery(_repositoryId, TypeFilter: BacklogItemType.UserStory),
            CancellationToken.None);

        result.Should().ContainSingle(
            "an Epic is only kept when it leads to a match, not merely for being an Epic");
        result[0].Title.Should().Be("Matched epic");
    }

    [Fact]
    public async Task Handle_KeptAncestorsDoNotPullInTheirOtherChildren()
    {
        var epic = await AddItemAsync("Epic", 1000m, BacklogItemType.Epic);
        await AddItemAsync("Ready story", 1000m, BacklogItemType.UserStory,
            BacklogItemState.Ready, parentId: epic.Id);
        await AddItemAsync("New story", 2000m, BacklogItemType.UserStory,
            BacklogItemState.New, parentId: epic.Id);

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new ListBacklogItemsQuery(_repositoryId, StateFilter: BacklogItemState.Ready),
            CancellationToken.None);

        result[0].Children.Should().ContainSingle(
            "only the matching sibling is shown — the ancestor is context, not a free pass");
        result[0].Children[0].Title.Should().Be("Ready story");
    }

    [Fact]
    public async Task Handle_WhenNothingMatches_ReturnsEmptyEvenWithAncestorsPresent()
    {
        var epic = await AddItemAsync("Epic", 1000m, BacklogItemType.Epic);
        await AddItemAsync("Story", 1000m, BacklogItemType.UserStory,
            BacklogItemState.New, parentId: epic.Id);

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new ListBacklogItemsQuery(_repositoryId, StateFilter: BacklogItemState.Committed),
            CancellationToken.None);

        result.Should().BeEmpty();
    }

    // ── Projection ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ProjectsTheSprintNameWhenTheItemIsScheduled()
    {
        var sprintId = Guid.NewGuid();
        _db.Set<Sprint>().Add(new Sprint
        {
            RepositoryId = _repositoryId,
            Name         = "Sprint 7",
            StartDate    = new DateOnly(2026, 5, 4),
            EndDate      = new DateOnly(2026, 5, 15),
        }.WithId(sprintId));
        await _db.SaveChangesAsync();

        var item = await AddItemAsync("Scheduled", 1000m);
        item.SprintId = sprintId;
        await _db.SaveChangesAsync();

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(new ListBacklogItemsQuery(_repositoryId), CancellationToken.None);

        result[0].SprintId.Should().Be(sprintId);
        result[0].SprintName.Should().Be("Sprint 7");
    }

    [Fact]
    public async Task Handle_LeavesTheSprintNameNullForUnscheduledItems()
    {
        await AddItemAsync("Unscheduled", 1000m);

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(new ListBacklogItemsQuery(_repositoryId), CancellationToken.None);

        result[0].SprintId.Should().BeNull();
        result[0].SprintName.Should().BeNull();
    }
}
