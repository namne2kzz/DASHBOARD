using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.SprintTasks.Commands.DeleteSprintTask;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.UnitTests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.UnitTests.Application.SprintTasks.Commands;

/// <summary>Unit tests for <see cref="DeleteSprintTaskCommandHandler"/>.</summary>
public sealed class DeleteSprintTaskCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();
    private readonly Guid                     _userId       = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public DeleteSprintTaskCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private DeleteSprintTaskCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private IRequestUserContext AuthorizedUser() =>
        RequestUserContextMock.ForUser(_userId)
            .WithPrivilege(SystemFunction.DeleteWorkItem, _repositoryId).Object;

    private async Task<SprintTask> AddTaskAsync(
        SprintTaskType type           = SprintTaskType.Task,
        Guid?          parentId       = null,
        Guid?          backlogItemId  = null,
        int            workItemNumber = 1,
        Guid?          repositoryId   = null)
    {
        var task = new SprintTask
        {
            RepositoryId   = repositoryId ?? _repositoryId,
            Type           = type,
            Title          = "Task",
            ParentId       = parentId,
            BacklogItemId  = backlogItemId,
            WorkItemNumber = workItemNumber,
        };
        _db.Set<SprintTask>().Add(task);
        await _db.SaveChangesAsync();
        return task;
    }

    /// <summary>Reads a task bypassing the soft-delete filter, so removed rows stay visible.</summary>
    private Task<SprintTask> LoadAsync(Guid id) =>
        _db.Set<SprintTask>().IgnoreQueryFilters().AsNoTracking().SingleAsync(t => t.Id == id);

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutDeleteWorkItemPrivilege_FailsAndKeepsTheTask()
    {
        var task    = await AddTaskAsync();
        var handler = CreateHandler(
            RequestUserContextMock.ForUser(_userId).AsMember(_repositoryId).Object);

        var act = () => handler.Handle(
            new DeleteSprintTaskCommand(_repositoryId, task.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await LoadAsync(task.Id)).IsDeleted.Should().BeFalse();
    }

    // ── Not found ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheTaskDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new DeleteSprintTaskCommand(_repositoryId, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenTheTaskBelongsToAnotherRepository_ThrowsNotFound()
    {
        var foreign = await AddTaskAsync(repositoryId: Guid.NewGuid());
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new DeleteSprintTaskCommand(_repositoryId, foreign.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Soft delete ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_SoftDeletesTheTaskAndStampsTheAudit()
    {
        var task    = await AddTaskAsync();
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new DeleteSprintTaskCommand(_repositoryId, task.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var deleted = await LoadAsync(task.Id);
        deleted.IsDeleted.Should().BeTrue();
        deleted.DeletedAt.Should().NotBeNull();
        deleted.DeletedByUserId.Should().Be(_userId, "the acting user is recorded");
        deleted.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_AfterDeletion_TheTaskDropsOutOfOrdinaryQueries()
    {
        var task    = await AddTaskAsync();
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new DeleteSprintTaskCommand(_repositoryId, task.Id), CancellationToken.None);

        (await _db.Set<SprintTask>().AsNoTracking().AnyAsync(t => t.Id == task.Id))
            .Should().BeFalse("the global soft-delete filter hides it");
    }

    // ── Cascade to sub-tasks ────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AlsoSoftDeletesTheSubTasks()
    {
        var story = await AddTaskAsync(SprintTaskType.UserStory, workItemNumber: 1);
        var subA  = await AddTaskAsync(parentId: story.Id, workItemNumber: 2);
        var subB  = await AddTaskAsync(parentId: story.Id, workItemNumber: 3);

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new DeleteSprintTaskCommand(_repositoryId, story.Id), CancellationToken.None);

        (await LoadAsync(subA.Id)).IsDeleted.Should().BeTrue(
            "a sub-task without its story would be unreachable");
        (await LoadAsync(subB.Id)).IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DeletingASubTaskLeavesItsParentAlone()
    {
        var story = await AddTaskAsync(SprintTaskType.UserStory, workItemNumber: 1);
        var sub   = await AddTaskAsync(parentId: story.Id, workItemNumber: 2);

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new DeleteSprintTaskCommand(_repositoryId, sub.Id), CancellationToken.None);

        (await LoadAsync(story.Id)).IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DoesNotTouchUnrelatedTasks()
    {
        var target    = await AddTaskAsync(workItemNumber: 1);
        var bystander = await AddTaskAsync(workItemNumber: 2);

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new DeleteSprintTaskCommand(_repositoryId, target.Id), CancellationToken.None);

        (await LoadAsync(bystander.Id)).IsDeleted.Should().BeFalse();
    }

    // ── Backlog restoration ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenAPromotedStoryIsDeleted_ReturnsTheBacklogItemToReady()
    {
        var backlogItem = new BacklogItem
        {
            RepositoryId = _repositoryId,
            Type         = BacklogItemType.UserStory,
            Title        = "The story",
            State        = BacklogItemState.Committed,
            Rank         = 1000m,
        };
        _db.Set<BacklogItem>().Add(backlogItem);
        await _db.SaveChangesAsync();

        var story = await AddTaskAsync(SprintTaskType.UserStory, backlogItemId: backlogItem.Id);

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new DeleteSprintTaskCommand(_repositoryId, story.Id), CancellationToken.None);

        var restored = await _db.Set<BacklogItem>().AsNoTracking()
            .SingleAsync(b => b.Id == backlogItem.Id);
        restored.State.Should().Be(BacklogItemState.Ready,
            "un-promoting must put the story back where it can be planned again");
        restored.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenTheDeletedItemIsNotAUserStory_LeavesTheBacklogAlone()
    {
        var backlogItem = new BacklogItem
        {
            RepositoryId = _repositoryId,
            Type         = BacklogItemType.UserStory,
            Title        = "The story",
            State        = BacklogItemState.Committed,
            Rank         = 1000m,
        };
        _db.Set<BacklogItem>().Add(backlogItem);
        await _db.SaveChangesAsync();

        // A Task carrying a backlog link is not a promotion, so nothing is restored.
        var task = await AddTaskAsync(SprintTaskType.Task, backlogItemId: backlogItem.Id);

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new DeleteSprintTaskCommand(_repositoryId, task.Id), CancellationToken.None);

        var unchanged = await _db.Set<BacklogItem>().AsNoTracking()
            .SingleAsync(b => b.Id == backlogItem.Id);
        unchanged.State.Should().Be(BacklogItemState.Committed);
    }

    [Fact]
    public async Task Handle_WhenTheStoryHasNoBacklogLink_Succeeds()
    {
        var story   = await AddTaskAsync(SprintTaskType.UserStory, backlogItemId: null);
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new DeleteSprintTaskCommand(_repositoryId, story.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("a story created directly on the board has nothing to restore");
    }
}
