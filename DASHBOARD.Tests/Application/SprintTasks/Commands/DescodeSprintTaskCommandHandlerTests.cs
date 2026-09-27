using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.SprintTasks.Commands.DescodeSprintTask;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Tests.Application.SprintTasks.Commands;

/// <summary>
/// Unit tests for <see cref="DescodeSprintTaskCommandHandler"/> — pulling a story back out of a sprint.
/// </summary>
/// <remarks>
/// Descoping is the inverse of <c>PromoteToSprint</c>: the work item and its sub-tasks leave the
/// sprint and the originating backlog item returns to Ready so it can be planned into a later one.
/// The handler calls <c>Remove</c>, but <see cref="SprintTask"/> implements <c>ISoftDelete</c>, so
/// the context's <c>SaveChangesAsync</c> override turns that into a soft delete — the rows survive
/// behind the global query filter rather than being erased.
/// </remarks>
public sealed class DescodeSprintTaskCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();
    private readonly Guid                     _sprintId     = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public DescodeSprintTaskCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private DescodeSprintTaskCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private IRequestUserContext AuthorizedUser() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.PromoteToSprint, _repositoryId).Object;

    private async Task<SprintTask> AddTaskAsync(
        SprintTaskType type           = SprintTaskType.UserStory,
        Guid?          parentId       = null,
        Guid?          backlogItemId  = null,
        Guid?          sprintId       = null,
        int            workItemNumber = 1)
    {
        var task = new SprintTask
        {
            RepositoryId   = _repositoryId,
            SprintId       = sprintId ?? _sprintId,
            Type           = type,
            Title          = "Story",
            ParentId       = parentId,
            BacklogItemId  = backlogItemId,
            WorkItemNumber = workItemNumber,
        };
        _db.Set<SprintTask>().Add(task);
        await _db.SaveChangesAsync();
        return task;
    }

    private async Task<BacklogItem> AddBacklogItemAsync(
        BacklogItemState state = BacklogItemState.Committed)
    {
        var item = new BacklogItem
        {
            RepositoryId = _repositoryId,
            Type         = BacklogItemType.UserStory,
            Title        = "The story",
            State        = state,
            Rank         = 1000m,
            SprintId     = _sprintId,
        };
        _db.Set<BacklogItem>().Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutPromoteToSprintPrivilege_FailsAndKeepsTheTask()
    {
        var task    = await AddTaskAsync();
        var handler = CreateHandler(
            RequestUserContextMock.ForUser().AsMember(_repositoryId).Object);

        var result = await handler.Handle(
            new DescodeSprintTaskCommand(_repositoryId, _sprintId, task.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue("descoping uses the same privilege as promoting");
        result.Error.Should().Contain("permission");
        (await _db.Set<SprintTask>().CountAsync()).Should().Be(1);
    }

    // ── Not found ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheTaskDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new DescodeSprintTaskCommand(_repositoryId, _sprintId, Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenTheTaskBelongsToAnotherSprint_ThrowsNotFound()
    {
        var task    = await AddTaskAsync(sprintId: Guid.NewGuid());
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new DescodeSprintTaskCommand(_repositoryId, _sprintId, task.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Root-level rule ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheTaskHasAParent_Fails()
    {
        var story = await AddTaskAsync(workItemNumber: 1);
        var sub   = await AddTaskAsync(SprintTaskType.Task, parentId: story.Id, workItemNumber: 2);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new DescodeSprintTaskCommand(_repositoryId, _sprintId, sub.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("root-level tasks");
        (await _db.Set<SprintTask>().CountAsync()).Should().Be(2, "nothing is removed");
    }

    // ── Removal ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_TakesTheStoryOffTheBoard()
    {
        var task    = await AddTaskAsync();
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new DescodeSprintTaskCommand(_repositoryId, _sprintId, task.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        (await _db.Set<SprintTask>().AnyAsync(t => t.Id == task.Id)).Should().BeFalse(
            "the story disappears from every ordinary query");

        var row = await _db.Set<SprintTask>().IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(t => t.Id == task.Id);
        row.IsDeleted.Should().BeTrue("SprintTask is soft-deleted, so the row is kept for audit");
        row.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_AlsoTakesTheSubTasksOffTheBoard()
    {
        var story = await AddTaskAsync(workItemNumber: 1);
        await AddTaskAsync(SprintTaskType.Task, parentId: story.Id, workItemNumber: 2);
        await AddTaskAsync(SprintTaskType.Task, parentId: story.Id, workItemNumber: 3);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new DescodeSprintTaskCommand(_repositoryId, _sprintId, story.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _db.Set<SprintTask>().CountAsync()).Should()
            .Be(0, "sub-tasks go with their story, they have nowhere to live otherwise");

        var rows = await _db.Set<SprintTask>().IgnoreQueryFilters().AsNoTracking().ToListAsync();
        rows.Should().HaveCount(3).And.OnlyContain(t => t.IsDeleted);
    }

    [Fact]
    public async Task Handle_LeavesOtherStoriesInTheSprintAlone()
    {
        var target    = await AddTaskAsync(workItemNumber: 1);
        var bystander = await AddTaskAsync(workItemNumber: 2);

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new DescodeSprintTaskCommand(_repositoryId, _sprintId, target.Id), CancellationToken.None);

        (await _db.Set<SprintTask>().AnyAsync(t => t.Id == bystander.Id)).Should().BeTrue();
    }

    // ── Backlog restoration ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsTheOriginatingBacklogItemToReady()
    {
        var backlogItem = await AddBacklogItemAsync(BacklogItemState.Committed);
        var task        = await AddTaskAsync(backlogItemId: backlogItem.Id);

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new DescodeSprintTaskCommand(_repositoryId, _sprintId, task.Id), CancellationToken.None);

        var restored = await _db.Set<BacklogItem>().AsNoTracking()
            .SingleAsync(b => b.Id == backlogItem.Id);
        restored.State.Should().Be(BacklogItemState.Ready,
            "the story goes back to the top of the refined backlog, ready to re-plan");
        restored.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_KeepsTheBacklogItemItself()
    {
        var backlogItem = await AddBacklogItemAsync();
        var task        = await AddTaskAsync(backlogItemId: backlogItem.Id);

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new DescodeSprintTaskCommand(_repositoryId, _sprintId, task.Id), CancellationToken.None);

        (await _db.Set<BacklogItem>().AnyAsync(b => b.Id == backlogItem.Id)).Should()
            .BeTrue("only the sprint work item is discarded, never the backlog entry");
    }

    [Fact]
    public async Task Handle_WhenTheStoryWasCreatedDirectlyOnTheBoard_Succeeds()
    {
        var task    = await AddTaskAsync(backlogItemId: null);
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new DescodeSprintTaskCommand(_repositoryId, _sprintId, task.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("there is simply nothing to restore");
    }

    [Fact]
    public async Task Handle_WhenTheBacklogItemWasDeleted_StillSucceeds()
    {
        // The link points at a row that no longer exists — the descope must not fail on it.
        var task    = await AddTaskAsync(backlogItemId: Guid.NewGuid());
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new DescodeSprintTaskCommand(_repositoryId, _sprintId, task.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}
