using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.SprintTasks.Queries.GetSprintTaskMetadata;
using DASHBOARD.Application.SprintTasks.Queries.SearchParentStories;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Tests.Common;

namespace DASHBOARD.Tests.Application.SprintTasks.Queries;

/// <summary>
/// Unit tests for <see cref="SearchParentStoriesQueryHandler"/> (the parent picker) and
/// <see cref="GetSprintTaskMetadataQueryHandler"/> (a work item's assigned catalog values).
/// </summary>
public sealed class ParentStoryAndMetadataQueryTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();
    private readonly Guid                     _taskId       = Guid.NewGuid();

    /// <summary>Sets up an isolated database holding the repository and one work item.</summary>
    public ParentStoryAndMetadataQueryTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;

        _db.Set<Repository>().Add(
            new Repository { Name = "Dashboard", Code = "DASH" }.WithId(_repositoryId));
        _db.Set<SprintTask>().Add(new SprintTask
        {
            RepositoryId = _repositoryId,
            Type         = SprintTaskType.Task,
            Title        = "The task",
        }.WithId(_taskId));
        _db.SaveChanges();
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private SearchParentStoriesQueryHandler SearchHandler(IRequestUserContext user) => new(_db, user);

    private GetSprintTaskMetadataQueryHandler MetadataHandler(IRequestUserContext user) => new(_db, user);

    private IRequestUserContext MemberUser() =>
        RequestUserContextMock.ForUser().AsMember(_repositoryId).Object;

    private async Task<SprintTask> AddStoryAsync(
        string         title          = "A story",
        SprintTaskType type           = SprintTaskType.UserStory,
        int            workItemNumber = 1,
        Guid?          repositoryId   = null,
        bool           isDeleted      = false)
    {
        var task = new SprintTask
        {
            RepositoryId   = repositoryId ?? _repositoryId,
            Type           = type,
            Title          = title,
            WorkItemNumber = workItemNumber,
            IsDeleted      = isDeleted,
            DeletedAt      = isDeleted ? DateTime.UtcNow : null,
        };
        _db.Set<SprintTask>().Add(task);
        await _db.SaveChangesAsync();
        return task;
    }

    private async Task<RepositoryMetadata> AddCatalogValueAsync(
        string value, MetadataKey key = MetadataKey.Labels)
    {
        var entry = new RepositoryMetadata
        {
            RepositoryId = _repositoryId,
            IsGlobal     = false,
            Key          = key,
            Value        = value,
        };
        _db.Set<RepositoryMetadata>().Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }

    private async Task AttachMetadataAsync(Guid metadataId, Guid? taskId = null)
    {
        _db.Set<WorkItemMetadata>().Add(new WorkItemMetadata
        {
            SprintTaskId = taskId ?? _taskId,
            MetadataId   = metadataId,
        });
        await _db.SaveChangesAsync();
    }

    // ── SearchParentStories: access control ─────────────────────────────────

    [Fact]
    public async Task Search_WhenTheUserIsNotAMember_Throws()
    {
        var handler = SearchHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(
            new SearchParentStoriesQuery(_repositoryId, null), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── SearchParentStories: candidate set ──────────────────────────────────

    [Fact]
    public async Task Search_ReturnsOnlyUserStories()
    {
        await AddStoryAsync("A story", SprintTaskType.UserStory, 1);
        await AddStoryAsync("A bug",   SprintTaskType.Bug,       2);

        var result = await SearchHandler(MemberUser()).Handle(
            new SearchParentStoriesQuery(_repositoryId, null), CancellationToken.None);

        result.Should().ContainSingle().Which.Title.Should().Be("A story",
            "only a User Story may act as a parent");
    }

    [Fact]
    public async Task Search_ExcludesStoriesOfOtherRepositories()
    {
        await AddStoryAsync("Mine",    workItemNumber: 1);
        await AddStoryAsync("Foreign", workItemNumber: 2, repositoryId: Guid.NewGuid());

        var result = await SearchHandler(MemberUser()).Handle(
            new SearchParentStoriesQuery(_repositoryId, null), CancellationToken.None);

        result.Should().ContainSingle().Which.Title.Should().Be("Mine");
    }

    [Fact]
    public async Task Search_ExcludesDeletedStories()
    {
        await AddStoryAsync("Live",    workItemNumber: 1);
        await AddStoryAsync("Deleted", workItemNumber: 2, isDeleted: true);

        var result = await SearchHandler(MemberUser()).Handle(
            new SearchParentStoriesQuery(_repositoryId, null), CancellationToken.None);

        result.Should().ContainSingle().Which.Title.Should().Be("Live");
    }

    // ── SearchParentStories: matching ───────────────────────────────────────

    [Fact]
    public async Task Search_WithNoTerm_ReturnsEveryCandidate()
    {
        await AddStoryAsync("First",  workItemNumber: 1);
        await AddStoryAsync("Second", workItemNumber: 2);

        var result = await SearchHandler(MemberUser()).Handle(
            new SearchParentStoriesQuery(_repositoryId, null), CancellationToken.None);

        result.Should().HaveCount(2, "an empty picker shows everything to choose from");
    }

    [Theory]
    [InlineData("checkout")]
    [InlineData("CHECKOUT")]
    [InlineData("  Checkout  ")]
    public async Task Search_MatchesTheTitleCaseInsensitively(string term)
    {
        await AddStoryAsync("Checkout flow", workItemNumber: 1);
        await AddStoryAsync("Unrelated",     workItemNumber: 2);

        var result = await SearchHandler(MemberUser()).Handle(
            new SearchParentStoriesQuery(_repositoryId, term), CancellationToken.None);

        result.Should().ContainSingle().Which.Title.Should().Be("Checkout flow");
    }

    [Theory]
    // the full work-item key
    [InlineData("DASH-7")]
    // just the number, which still appears inside the key
    [InlineData("7")]
    public async Task Search_MatchesTheWorkItemKey(string term)
    {
        await AddStoryAsync("Opaque title", workItemNumber: 7);

        var result = await SearchHandler(MemberUser()).Handle(
            new SearchParentStoriesQuery(_repositoryId, term), CancellationToken.None);

        result.Should().ContainSingle();
    }

    [Fact]
    public async Task Search_WhenNothingMatches_ReturnsEmpty()
    {
        await AddStoryAsync("Checkout flow");

        var result = await SearchHandler(MemberUser()).Handle(
            new SearchParentStoriesQuery(_repositoryId, "zzzz"), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_CapsResultsAtTwenty()
    {
        for (var i = 1; i <= 25; i++)
            await AddStoryAsync($"Story {i}", workItemNumber: i);

        var result = await SearchHandler(MemberUser()).Handle(
            new SearchParentStoriesQuery(_repositoryId, null), CancellationToken.None);

        result.Should().HaveCount(20, "the picker is a shortlist, not a full listing");
    }

    [Fact]
    public async Task Search_ProjectsTheWorkItemKey()
    {
        await AddStoryAsync("A story", workItemNumber: 7);

        var result = await SearchHandler(MemberUser()).Handle(
            new SearchParentStoriesQuery(_repositoryId, null), CancellationToken.None);

        result[0].WorkItemNumber.Should().Be(SprintTask.BuildWorkItemNumber("DASH", 7));
    }

    // ── GetSprintTaskMetadata ───────────────────────────────────────────────

    [Fact]
    public async Task Metadata_WhenTheUserIsNotAMember_Throws()
    {
        var handler = MetadataHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(
            new GetSprintTaskMetadataQuery(_repositoryId, _taskId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Metadata_WithNothingAttached_ReturnsEmpty()
    {
        var result = await MetadataHandler(MemberUser()).Handle(
            new GetSprintTaskMetadataQuery(_repositoryId, _taskId), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Metadata_ReturnsTheAttachedValuesWithTheirKeyLabel()
    {
        var label = await AddCatalogValueAsync("urgent");
        await AttachMetadataAsync(label.Id);

        var result = await MetadataHandler(MemberUser()).Handle(
            new GetSprintTaskMetadataQuery(_repositoryId, _taskId), CancellationToken.None);

        var item = result.Should().ContainSingle().Subject;
        item.MetadataId.Should().Be(label.Id);
        item.Value.Should().Be("urgent");
        item.Key.Should().Be((int)MetadataKey.Labels);
        item.KeyName.Should().NotBeNullOrWhiteSpace(
            "the UI groups chips under a human-readable key name");
    }

    [Fact]
    public async Task Metadata_OrdersByKeyThenValue()
    {
        var labelB   = await AddCatalogValueAsync("beta",  MetadataKey.Labels);
        var labelA   = await AddCatalogValueAsync("alpha", MetadataKey.Labels);
        var version  = await AddCatalogValueAsync("v1.0",  MetadataKey.FixedInVersion);
        await AttachMetadataAsync(labelB.Id);
        await AttachMetadataAsync(labelA.Id);
        await AttachMetadataAsync(version.Id);

        var result = await MetadataHandler(MemberUser()).Handle(
            new GetSprintTaskMetadataQuery(_repositoryId, _taskId), CancellationToken.None);

        // FixedInVersion precedes Labels in the enum, and values sort alphabetically inside a key.
        result.Select(r => r.Value).Should().ContainInOrder("v1.0", "alpha", "beta");
    }

    [Fact]
    public async Task Metadata_ReturnsOnlyTheRequestedWorkItemsValues()
    {
        var otherTaskId = Guid.NewGuid();
        _db.Set<SprintTask>().Add(new SprintTask
        {
            RepositoryId = _repositoryId,
            Type         = SprintTaskType.Task,
            Title        = "Other",
        }.WithId(otherTaskId));
        await _db.SaveChangesAsync();

        var mine   = await AddCatalogValueAsync("mine");
        var theirs = await AddCatalogValueAsync("theirs");
        await AttachMetadataAsync(mine.Id);
        await AttachMetadataAsync(theirs.Id, otherTaskId);

        var result = await MetadataHandler(MemberUser()).Handle(
            new GetSprintTaskMetadataQuery(_repositoryId, _taskId), CancellationToken.None);

        result.Should().ContainSingle().Which.Value.Should().Be("mine");
    }
}
