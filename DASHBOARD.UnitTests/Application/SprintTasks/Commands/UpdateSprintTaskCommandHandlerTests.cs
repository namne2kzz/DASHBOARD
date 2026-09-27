using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.SprintTasks.Commands.UpdateSprintTask;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.UnitTests.Common;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DASHBOARD.UnitTests.Application.SprintTasks.Commands;

/// <summary>
/// Unit tests for <see cref="UpdateSprintTaskCommandHandler"/>.
/// </summary>
/// <remarks>
/// BUG-022 (High) was a privilege hole here: this handler checked only <c>IsMemberOfAsync</c> and
/// never <see cref="SystemFunction.EditWorkItem"/>, unlike every sibling handler in the feature.
/// Any signed-in member could rewrite any work item's fields. The permission test is the
/// regression guard — it must assert the privilege, not mere membership.
/// </remarks>
public sealed class UpdateSprintTaskCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Mock<IHistoryService>    _history = new();
    private readonly Guid                     _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated database holding the owning repository.</summary>
    public UpdateSprintTaskCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;

        _db.Set<Repository>().Add(
            new Repository { Name = "Dashboard", Code = "DASH" }.WithId(_repositoryId));
        _db.SaveChanges();
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private UpdateSprintTaskCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _history.Object, _database.Uow);

    private IRequestUserContext AuthorizedUser() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.EditWorkItem, _repositoryId).Object;

    private async Task<SprintTask> AddTaskAsync(
        SprintTaskType type         = SprintTaskType.Task,
        string         title        = "Original title",
        Guid?          assignedToId = null,
        Guid?          parentId     = null,
        int            workItemNumber = 1,
        Guid?          repositoryId = null)
    {
        var task = new SprintTask
        {
            RepositoryId   = repositoryId ?? _repositoryId,
            Type           = type,
            Title          = title,
            Description    = "Original description",
            Priority       = WorkItemPriority.Medium,
            AssignedToId   = assignedToId,
            ParentId       = parentId,
            WorkItemNumber = workItemNumber,
        };
        _db.Set<SprintTask>().Add(task);
        await _db.SaveChangesAsync();
        return task;
    }

    private UpdateSprintTaskCommand Command(
        Guid             taskId,
        string           title            = "Original title",
        string           description      = "Original description",
        WorkItemPriority priority         = WorkItemPriority.Medium,
        Guid?            assignedToId     = null,
        int              storyPoints      = 0,
        decimal          originalEstimate = 0m,
        decimal          remainingWork    = 0m,
        Guid?            parentId         = null) =>
        new(_repositoryId, taskId, title, description, priority, assignedToId,
            storyPoints, originalEstimate,
            null, null, null, null, null, null, null, null, null, null, null,
            remainingWork, parentId);

    private Task<SprintTask> LoadAsync(Guid id) =>
        _db.Set<SprintTask>().AsNoTracking().SingleAsync(t => t.Id == id);

    // ── BUG-022 regression: EditWorkItem privilege, not just membership ─────

    [Fact]
    public async Task Handle_WithoutEditWorkItemPrivilege_FailsAndLeavesTheTaskUnchanged()
    {
        var task = await AddTaskAsync();

        // A plain member with no EditWorkItem privilege — exactly the BUG-022 scenario.
        var handler = CreateHandler(
            RequestUserContextMock.ForUser().AsMember(_repositoryId).Object);

        var act = () => handler.Handle(
            Command(task.Id, title: "Hijacked"), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();

        var unchanged = await LoadAsync(task.Id);
        unchanged.Title.Should().Be("Original title");
    }

    [Fact]
    public async Task Handle_WithEditWorkItemPrivilege_Succeeds()
    {
        var task    = await AddTaskAsync();
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(task.Id, title: "Updated"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // ── Not found ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTaskDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(Command(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenTaskBelongsToAnotherRepository_ThrowsNotFound()
    {
        var foreign = await AddTaskAsync(repositoryId: Guid.NewGuid());
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(Command(foreign.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Parent rules ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTaskIsMadeItsOwnParent_Fails()
    {
        var task    = await AddTaskAsync();
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(task.Id, parentId: task.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("its own parent");
    }

    [Fact]
    public async Task Handle_WhenParentIsNotAUserStory_Fails()
    {
        var task      = await AddTaskAsync(workItemNumber: 1);
        var badParent = await AddTaskAsync(SprintTaskType.Bug, "A bug", workItemNumber: 2);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(task.Id, parentId: badParent.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("must be a User Story");
    }

    [Fact]
    public async Task Handle_WhenParentDoesNotExist_ThrowsNotFound()
    {
        var task    = await AddTaskAsync();
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            Command(task.Id, parentId: Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WithAUserStoryParent_LinksTheTask()
    {
        var task  = await AddTaskAsync(workItemNumber: 1);
        var story = await AddTaskAsync(SprintTaskType.UserStory, "The story", workItemNumber: 2);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(task.Id, parentId: story.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await LoadAsync(task.Id)).ParentId.Should().Be(story.Id);
    }

    [Fact]
    public async Task Handle_WithANullParent_ClearsTheLink()
    {
        var story = await AddTaskAsync(SprintTaskType.UserStory, "The story", workItemNumber: 2);
        var task  = await AddTaskAsync(parentId: story.Id, workItemNumber: 1);

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(Command(task.Id, parentId: null), CancellationToken.None);

        (await LoadAsync(task.Id)).ParentId.Should().BeNull();
    }

    // ── Assignee rule for User Stories ──────────────────────────────────────

    [Fact]
    public async Task Handle_ForAUserStory_SilentlyDropsTheAssignee()
    {
        var story   = await AddTaskAsync(SprintTaskType.UserStory, "The story");
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(story.Id, assignedToId: Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("the assignee is dropped rather than rejected");
        (await LoadAsync(story.Id)).AssignedToId.Should().BeNull(
            "a User Story is delivered by its child tasks, so it carries no assignee");
    }

    [Fact]
    public async Task Handle_ForATask_KeepsTheAssignee()
    {
        var assignee = Guid.NewGuid();
        var task     = await AddTaskAsync();
        var handler  = CreateHandler(AuthorizedUser());

        await handler.Handle(Command(task.Id, assignedToId: assignee), CancellationToken.None);

        (await LoadAsync(task.Id)).AssignedToId.Should().Be(assignee);
    }

    // ── Remaining work ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ClampsNegativeRemainingWorkToZero()
    {
        var task    = await AddTaskAsync();
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(Command(task.Id, remainingWork: -5m), CancellationToken.None);

        (await LoadAsync(task.Id)).RemainingWork.Should().Be(0m);
    }

    // ── Field persistence ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_PersistsTheUpdatedFieldsAndTimestamp()
    {
        var task    = await AddTaskAsync();
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            Command(task.Id, title: "New title", description: "New description",
                priority: WorkItemPriority.Critical, storyPoints: 8,
                originalEstimate: 12m, remainingWork: 4m),
            CancellationToken.None);

        var updated = await LoadAsync(task.Id);
        updated.Title.Should().Be("New title");
        updated.Description.Should().Be("New description");
        updated.Priority.Should().Be(WorkItemPriority.Critical);
        updated.StoryPoints.Should().Be(8);
        updated.OriginalEstimate.Should().Be(12m);
        updated.RemainingWork.Should().Be(4m);
        updated.UpdatedAt.Should().NotBeNull();
    }

    // ── History ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_RecordsOneHistoryEntryPerChangedField()
    {
        var task    = await AddTaskAsync();
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            Command(task.Id, title: "New title", priority: WorkItemPriority.High),
            CancellationToken.None);

        _history.Verify(h => h.Record(task.Id, _repositoryId, It.IsAny<Guid>(),
            "Title changed from 'Original title' to 'New title'."), Times.Once);
        _history.Verify(h => h.Record(task.Id, _repositoryId, It.IsAny<Guid>(),
            "Priority changed from 'Medium' to 'High'."), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNothingChanges_RecordsNoHistory()
    {
        var task    = await AddTaskAsync();
        var handler = CreateHandler(AuthorizedUser());

        // Every value matches what the task already holds.
        await handler.Handle(Command(task.Id), CancellationToken.None);

        _history.Verify(h => h.Record(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RecordsTheParentChangeWithWorkItemLabels()
    {
        var story = await AddTaskAsync(SprintTaskType.UserStory, "The story", workItemNumber: 7);
        var task  = await AddTaskAsync(workItemNumber: 1);

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(Command(task.Id, parentId: story.Id), CancellationToken.None);

        _history.Verify(h => h.Record(task.Id, _repositoryId, It.IsAny<Guid>(),
            It.Is<string>(m => m.StartsWith("Parent changed from 'None' to '")
                            && m.Contains("The story"))), Times.Once);
    }

    [Fact]
    public async Task Handle_RecordsRemainingWorkUsingTheClampedValue()
    {
        var task    = await AddTaskAsync();
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(Command(task.Id, remainingWork: -5m), CancellationToken.None);

        _history.Verify(h => h.Record(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.Is<string>(m => m.Contains("Remaining work"))), Times.Never,
            "the task already had 0h, and -5h clamps to 0h — so nothing actually changed");
    }
}
