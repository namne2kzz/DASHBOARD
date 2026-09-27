using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.SprintTasks.Commands.CreateSprintTask;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DASHBOARD.Tests.Application.SprintTasks.Commands;

/// <summary>Unit tests for <see cref="CreateSprintTaskCommandHandler"/>.</summary>
public sealed class CreateSprintTaskCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Mock<IHistoryService>    _history = new();
    private readonly Guid                     _repositoryId = Guid.NewGuid();
    private readonly Guid                     _sprintId     = Guid.NewGuid();

    /// <summary>Sets up an isolated database holding the repository and one sprint.</summary>
    public CreateSprintTaskCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;

        _db.Set<Repository>().Add(
            new Repository { Name = "Dashboard", Code = "DASH" }.WithId(_repositoryId));
        _db.Set<Sprint>().Add(new Sprint
        {
            RepositoryId = _repositoryId,
            Name         = "Sprint 1",
            StartDate    = new DateOnly(2026, 5, 4),
            EndDate      = new DateOnly(2026, 5, 15),
        }.WithId(_sprintId));
        _db.SaveChanges();
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private CreateSprintTaskCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _history.Object, _database.Uow);

    private IRequestUserContext AuthorizedUser() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.CreateWorkItem, _repositoryId).Object;

    private CreateSprintTaskCommand Command(
        SprintTaskType   type             = SprintTaskType.Task,
        Guid?            sprintId         = null,
        Guid?            parentId         = null,
        string           title            = "New task",
        Guid?            assignedToId     = null,
        int              storyPoints      = 0,
        decimal          originalEstimate = 0m,
        WorkItemPriority priority         = WorkItemPriority.Medium) =>
        new(_repositoryId, sprintId, parentId, type, title, "Description",
            priority, assignedToId, storyPoints, originalEstimate);

    private async Task<SprintTask> AddTaskAsync(
        SprintTaskType type = SprintTaskType.UserStory, int workItemNumber = 1, Guid? repositoryId = null)
    {
        var task = new SprintTask
        {
            RepositoryId   = repositoryId ?? _repositoryId,
            Type           = type,
            Title          = "Existing",
            WorkItemNumber = workItemNumber,
        };
        _db.Set<SprintTask>().Add(task);
        await _db.SaveChangesAsync();
        return task;
    }

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutCreateWorkItemPrivilege_Fails()
    {
        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("permission");
        (await _db.Set<SprintTask>().CountAsync()).Should().Be(0);
    }

    // ── User Story assignee rule ────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenAUserStoryIsGivenAnAssignee_Fails()
    {
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(SprintTaskType.UserStory, assignedToId: Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be assigned to an individual");
    }

    [Fact]
    public async Task Handle_AnUnassignedUserStoryIsAccepted()
    {
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(SprintTaskType.UserStory), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // ── Sprint and parent validation ────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheSprintDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            Command(sprintId: Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenTheParentDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            Command(parentId: Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenTheParentIsNotAUserStory_Fails()
    {
        var badParent = await AddTaskAsync(SprintTaskType.Bug);
        var handler   = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(parentId: badParent.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("must be a User Story");
    }

    [Fact]
    public async Task Handle_WithAUserStoryParent_NestsTheTask()
    {
        var story   = await AddTaskAsync(SprintTaskType.UserStory);
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(Command(parentId: story.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var created = await _db.Set<SprintTask>().AsNoTracking()
            .SingleAsync(t => t.Id == result.Value!.Id);
        created.ParentId.Should().Be(story.Id);
    }

    // ── Initial state ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenCreatedInASprint_StartsAsNew()
    {
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(Command(sprintId: _sprintId), CancellationToken.None);

        var created = await _db.Set<SprintTask>().AsNoTracking()
            .SingleAsync(t => t.Id == result.Value!.Id);
        created.State.Should().Be(SprintTaskState.New);
        created.StateChangedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenCreatedWithoutASprint_StartsInTheBacklog()
    {
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(Command(sprintId: null), CancellationToken.None);

        var created = await _db.Set<SprintTask>().AsNoTracking()
            .SingleAsync(t => t.Id == result.Value!.Id);
        created.State.Should().Be(SprintTaskState.Backlog,
            "unscheduled work waits in the backlog rather than appearing on a board");
    }

    // ── Work-item numbering ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NumbersTheFirstWorkItemOne()
    {
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(Command(), CancellationToken.None);

        var created = await _db.Set<SprintTask>().AsNoTracking()
            .SingleAsync(t => t.Id == result.Value!.Id);
        created.WorkItemNumber.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ContinuesTheRepositorysNumbering()
    {
        await AddTaskAsync(workItemNumber: 41);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(Command(), CancellationToken.None);

        var created = await _db.Set<SprintTask>().AsNoTracking()
            .SingleAsync(t => t.Id == result.Value!.Id);
        created.WorkItemNumber.Should().Be(42);
    }

    [Fact]
    public async Task Handle_NumbersPerRepositoryNotGlobally()
    {
        await AddTaskAsync(workItemNumber: 99, repositoryId: Guid.NewGuid());

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(Command(), CancellationToken.None);

        var created = await _db.Set<SprintTask>().AsNoTracking()
            .SingleAsync(t => t.Id == result.Value!.Id);
        created.WorkItemNumber.Should().Be(1, "another repository's numbering must not leak in");
    }

    // ── Estimates ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_SeedsRemainingWorkFromTheOriginalEstimate()
    {
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(originalEstimate: 12m), CancellationToken.None);

        var created = await _db.Set<SprintTask>().AsNoTracking()
            .SingleAsync(t => t.Id == result.Value!.Id);
        created.OriginalEstimate.Should().Be(12m);
        created.RemainingWork.Should().Be(12m, "nothing has been worked yet");
        created.CompletedWork.Should().Be(0m);
    }

    // ── Persistence ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_PersistsTheSuppliedContent()
    {
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(SprintTaskType.Bug, sprintId: _sprintId, title: "Login crashes",
                storyPoints: 5, priority: WorkItemPriority.Critical),
            CancellationToken.None);

        var created = await _db.Set<SprintTask>().AsNoTracking()
            .SingleAsync(t => t.Id == result.Value!.Id);
        created.RepositoryId.Should().Be(_repositoryId);
        created.SprintId.Should().Be(_sprintId);
        created.Type.Should().Be(SprintTaskType.Bug);
        created.Title.Should().Be("Login crashes");
        created.StoryPoints.Should().Be(5);
        created.Priority.Should().Be(WorkItemPriority.Critical);
    }

    // ── History ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_RecordsCreationHistory()
    {
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(title: "Login crashes"), CancellationToken.None);

        _history.Verify(h => h.Record(result.Value!.Id, _repositoryId, It.IsAny<Guid>(),
            It.IsAny<string>()), Times.AtLeastOnce);
    }
}
