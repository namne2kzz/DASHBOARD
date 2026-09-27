using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.SprintTasks.Queries.ListStandaloneItems;
using DASHBOARD.Application.SprintTasks.Queries.ResolveSprintTaskKey;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.UnitTests.Common;

namespace DASHBOARD.UnitTests.Application.SprintTasks.Queries;

/// <summary>
/// Unit tests for <see cref="ListStandaloneItemsQueryHandler"/> and
/// <see cref="ResolveSprintTaskKeyQueryHandler"/>.
/// </summary>
public sealed class SprintTaskQueryHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated database holding the repository under test.</summary>
    public SprintTaskQueryHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;

        _db.Set<Repository>().Add(
            new Repository { Name = "Dashboard", Code = "DASH" }.WithId(_repositoryId));
        _db.SaveChanges();
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private ListStandaloneItemsQueryHandler ListHandler(IRequestUserContext user) => new(_db, user);

    private ResolveSprintTaskKeyQueryHandler ResolveHandler(IRequestUserContext user) => new(_db, user);

    private IRequestUserContext MemberUser() =>
        RequestUserContextMock.ForUser().AsMember(_repositoryId).Object;

    private async Task<SprintTask> AddTaskAsync(
        string          title          = "Standalone",
        SprintTaskType  type           = SprintTaskType.Task,
        SprintTaskState state          = SprintTaskState.Backlog,
        Guid?           sprintId       = null,
        Guid?           parentId       = null,
        int             workItemNumber = 1,
        Guid?           repositoryId   = null,
        DateTime?       createdAt      = null)
    {
        var task = new SprintTask
        {
            RepositoryId   = repositoryId ?? _repositoryId,
            SprintId       = sprintId,
            ParentId       = parentId,
            Type           = type,
            State          = state,
            Title          = title,
            WorkItemNumber = workItemNumber,
        };
        _db.Set<SprintTask>().Add(task);
        await _db.SaveChangesAsync();
        return task;
    }

    // ── ListStandaloneItems: access control ─────────────────────────────────

    [Fact]
    public async Task List_WhenTheUserIsNotAMember_Throws()
    {
        var handler = ListHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(
            new ListStandaloneItemsQuery(_repositoryId), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    // ── ListStandaloneItems: what counts as standalone ──────────────────────

    [Fact]
    public async Task List_ReturnsItemsWithNoSprintAndNoParent()
    {
        await AddTaskAsync("Standalone");

        var result = await ListHandler(MemberUser()).Handle(
            new ListStandaloneItemsQuery(_repositoryId), CancellationToken.None);

        result.Should().ContainSingle().Which.Title.Should().Be("Standalone");
    }

    [Fact]
    public async Task List_ExcludesItemsAssignedToASprint()
    {
        await AddTaskAsync("Scheduled", sprintId: Guid.NewGuid());

        var result = await ListHandler(MemberUser()).Handle(
            new ListStandaloneItemsQuery(_repositoryId), CancellationToken.None);

        result.Should().BeEmpty("work in a sprint belongs to that sprint's board");
    }

    [Fact]
    public async Task List_ExcludesSubTasks()
    {
        var story = await AddTaskAsync("Story", SprintTaskType.UserStory, workItemNumber: 1);
        await AddTaskAsync("Sub", parentId: story.Id, workItemNumber: 2);

        var result = await ListHandler(MemberUser()).Handle(
            new ListStandaloneItemsQuery(_repositoryId), CancellationToken.None);

        result.Should().ContainSingle().Which.Title.Should().Be("Story",
            "a sub-task is shown under its parent, not on its own");
    }

    [Fact]
    public async Task List_ExcludesItemsOfOtherRepositories()
    {
        await AddTaskAsync("Foreign", repositoryId: Guid.NewGuid());

        var result = await ListHandler(MemberUser()).Handle(
            new ListStandaloneItemsQuery(_repositoryId), CancellationToken.None);

        result.Should().BeEmpty();
    }

    // ── ListStandaloneItems: filters ────────────────────────────────────────

    [Fact]
    public async Task List_WithATypeFilter_ReturnsOnlyThatType()
    {
        await AddTaskAsync("A bug",  SprintTaskType.Bug,  workItemNumber: 1);
        await AddTaskAsync("A task", SprintTaskType.Task, workItemNumber: 2);

        var result = await ListHandler(MemberUser()).Handle(
            new ListStandaloneItemsQuery(_repositoryId, Type: SprintTaskType.Bug),
            CancellationToken.None);

        result.Should().ContainSingle().Which.Title.Should().Be("A bug");
    }

    [Fact]
    public async Task List_WithAStateFilter_ReturnsOnlyThatState()
    {
        await AddTaskAsync("Backlog item", state: SprintTaskState.Backlog, workItemNumber: 1);
        await AddTaskAsync("Active item",  state: SprintTaskState.Active,  workItemNumber: 2);

        var result = await ListHandler(MemberUser()).Handle(
            new ListStandaloneItemsQuery(_repositoryId, State: SprintTaskState.Active),
            CancellationToken.None);

        result.Should().ContainSingle().Which.Title.Should().Be("Active item");
    }

    // ── ListStandaloneItems: projection ─────────────────────────────────────

    [Fact]
    public async Task List_BuildsTheWorkItemKeyFromTheRepositoryCode()
    {
        await AddTaskAsync(workItemNumber: 7);

        var result = await ListHandler(MemberUser()).Handle(
            new ListStandaloneItemsQuery(_repositoryId), CancellationToken.None);

        result[0].WorkItemNumber.Should().Be(SprintTask.BuildWorkItemNumber("DASH", 7));
    }

    [Fact]
    public async Task List_CountsOnlyLiveSubTasks()
    {
        var story = await AddTaskAsync("Story", SprintTaskType.UserStory, workItemNumber: 1);
        await AddTaskAsync("Live",    parentId: story.Id, workItemNumber: 2);

        var deleted = await AddTaskAsync("Deleted", parentId: story.Id, workItemNumber: 3);
        deleted.IsDeleted = true;
        deleted.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var result = await ListHandler(MemberUser()).Handle(
            new ListStandaloneItemsQuery(_repositoryId), CancellationToken.None);

        result.Single(r => r.Title == "Story").SubTaskCount.Should().Be(1,
            "a deleted sub-task must not inflate the badge");
    }

    // ── ResolveSprintTaskKey ────────────────────────────────────────────────

    [Fact]
    public async Task Resolve_WhenTheUserIsNotAMember_Throws()
    {
        await AddTaskAsync(workItemNumber: 10);
        var handler = ResolveHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(
            new ResolveSprintTaskKeyQuery(_repositoryId, "DASH-10"), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Theory]
    // the full key
    [InlineData("DASH-10")]
    // lower case
    [InlineData("dash-10")]
    // the bare number
    [InlineData("10")]
    // with surrounding whitespace
    [InlineData("  DASH-10  ")]
    public async Task Resolve_FindsTheTaskFromVariousKeyForms(string key)
    {
        var task    = await AddTaskAsync(workItemNumber: 10);
        var handler = ResolveHandler(MemberUser());

        var result = await handler.Handle(
            new ResolveSprintTaskKeyQuery(_repositoryId, key), CancellationToken.None);

        result.Should().Be(task.Id);
    }

    [Theory]
    [InlineData("not-a-key")]
    [InlineData("DASH-0")]
    [InlineData("DASH--5")]
    [InlineData("")]
    public async Task Resolve_WithAnUnparseableKey_ThrowsNotFound(string key)
    {
        await AddTaskAsync(workItemNumber: 10);
        var handler = ResolveHandler(MemberUser());

        var act = () => handler.Handle(
            new ResolveSprintTaskKeyQuery(_repositoryId, key), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Resolve_WhenNoTaskCarriesThatNumber_ThrowsNotFound()
    {
        await AddTaskAsync(workItemNumber: 10);
        var handler = ResolveHandler(MemberUser());

        var act = () => handler.Handle(
            new ResolveSprintTaskKeyQuery(_repositoryId, "DASH-99"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Resolve_DoesNotReachIntoAnotherRepository()
    {
        await AddTaskAsync(workItemNumber: 10, repositoryId: Guid.NewGuid());
        var handler = ResolveHandler(MemberUser());

        var act = () => handler.Handle(
            new ResolveSprintTaskKeyQuery(_repositoryId, "DASH-10"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>(
            "work-item numbers repeat across repositories, so the lookup must stay scoped");
    }
}
