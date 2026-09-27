using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.SprintTasks.Commands.LogWork;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Tests.Application.SprintTasks.Commands;

/// <summary>Unit tests for <see cref="LogWorkCommandHandler"/>.</summary>
public sealed class LogWorkCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();
    private readonly Guid                     _sprintId     = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public LogWorkCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private LogWorkCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private IRequestUserContext AuthorizedUser() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.EditWorkItem, _repositoryId).Object;

    private async Task<SprintTask> AddTaskAsync(
        decimal         completedWork = 0m,
        decimal         remainingWork = 8m,
        SprintTaskState state         = SprintTaskState.Active,
        Guid?           sprintId      = null)
    {
        var task = new SprintTask
        {
            RepositoryId  = _repositoryId,
            SprintId      = sprintId ?? _sprintId,
            Type          = SprintTaskType.Task,
            Title         = "Task",
            State         = state,
            CompletedWork = completedWork,
            RemainingWork = remainingWork,
        };
        _db.Set<SprintTask>().Add(task);
        await _db.SaveChangesAsync();
        return task;
    }

    private Task<SprintTask> LoadAsync(Guid id) =>
        _db.Set<SprintTask>().AsNoTracking().SingleAsync(t => t.Id == id);

    // ── Access control ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutEditWorkItemPrivilege_Fails()
    {
        var task    = await AddTaskAsync();
        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var result = await handler.Handle(
            new LogWorkCommand(_repositoryId, _sprintId, task.Id, 3m, 5m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("permission");
        (await LoadAsync(task.Id)).CompletedWork.Should().Be(0m);
    }

    [Fact]
    public async Task Handle_ForAPlainMemberWithoutThePrivilege_CannotCloseTheTask()
    {
        // Logging work down to zero auto-closes the item, so membership alone must not be enough —
        // otherwise this is a back door around the EditWorkItem check on UpdateSprintTask.
        var task    = await AddTaskAsync(remainingWork: 3m, state: SprintTaskState.Active);
        var handler = CreateHandler(
            RequestUserContextMock.ForUser().AsMember(_repositoryId).Object);

        var result = await handler.Handle(
            new LogWorkCommand(_repositoryId, _sprintId, task.Id, 3m, 0m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();

        var untouched = await LoadAsync(task.Id);
        untouched.State.Should().Be(SprintTaskState.Active);
        untouched.ClosedAt.Should().BeNull();
    }

    // ── Input validation ────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Handle_WhenHoursWorkedIsNotPositive_Fails(decimal hours)
    {
        var task    = await AddTaskAsync();
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new LogWorkCommand(_repositoryId, _sprintId, task.Id, hours, 5m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("greater than zero");
    }

    [Fact]
    public async Task Handle_WhenRemainingWorkIsNegative_Fails()
    {
        var task    = await AddTaskAsync();
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new LogWorkCommand(_repositoryId, _sprintId, task.Id, 3m, -1m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be negative");
    }

    // ── Not found ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTaskDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new LogWorkCommand(_repositoryId, _sprintId, Guid.NewGuid(), 3m, 5m),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenTaskBelongsToAnotherSprint_ThrowsNotFound()
    {
        var task    = await AddTaskAsync(sprintId: Guid.NewGuid());
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new LogWorkCommand(_repositoryId, _sprintId, task.Id, 3m, 5m), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>(
            "the task is looked up by sprint, not by repository");
    }

    // ── Accumulating work ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AddsTheHoursToCompletedWork()
    {
        var task    = await AddTaskAsync(completedWork: 2m, remainingWork: 8m);
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new LogWorkCommand(_repositoryId, _sprintId, task.Id, 3m, 5m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = await LoadAsync(task.Id);
        updated.CompletedWork.Should().Be(5m, "logged hours accumulate rather than replace");
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ReplacesRemainingWorkWithTheSuppliedEstimate()
    {
        var task    = await AddTaskAsync(remainingWork: 8m);
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new LogWorkCommand(_repositoryId, _sprintId, task.Id, 3m, 6m), CancellationToken.None);

        (await LoadAsync(task.Id)).RemainingWork.Should().Be(6m,
            "remaining work is a fresh estimate, not a subtraction");
    }

    [Fact]
    public async Task Handle_AcceptsARemainingEstimateHigherThanBefore()
    {
        var task    = await AddTaskAsync(remainingWork: 4m);
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new LogWorkCommand(_repositoryId, _sprintId, task.Id, 2m, 10m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("work often reveals the task is bigger than thought");
        (await LoadAsync(task.Id)).RemainingWork.Should().Be(10m);
    }

    [Fact]
    public async Task Handle_LoggingTwiceKeepsAccumulating()
    {
        var task    = await AddTaskAsync(remainingWork: 10m);
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new LogWorkCommand(_repositoryId, _sprintId, task.Id, 3m, 7m), CancellationToken.None);
        await handler.Handle(
            new LogWorkCommand(_repositoryId, _sprintId, task.Id, 4m, 3m), CancellationToken.None);

        var updated = await LoadAsync(task.Id);
        updated.CompletedWork.Should().Be(7m);
        updated.RemainingWork.Should().Be(3m);
    }

    // ── Auto-close ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenRemainingReachesZero_AutoClosesTheTask()
    {
        var task    = await AddTaskAsync(remainingWork: 3m, state: SprintTaskState.Active);
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new LogWorkCommand(_repositoryId, _sprintId, task.Id, 3m, 0m), CancellationToken.None);

        var updated = await LoadAsync(task.Id);
        updated.State.Should().Be(SprintTaskState.Done);
        updated.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenRemainingIsAboveZero_LeavesTheStateAlone()
    {
        var task    = await AddTaskAsync(remainingWork: 8m, state: SprintTaskState.Active);
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new LogWorkCommand(_repositoryId, _sprintId, task.Id, 3m, 5m), CancellationToken.None);

        var updated = await LoadAsync(task.Id);
        updated.State.Should().Be(SprintTaskState.Active);
        updated.ClosedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenTheTaskIsAlreadyDone_DoesNotRestampClosedAt()
    {
        var originalClosedAt = DateTime.UtcNow.AddDays(-3);
        var task = await AddTaskAsync(remainingWork: 0m, state: SprintTaskState.Done);
        task.ClosedAt = originalClosedAt;
        await _db.SaveChangesAsync();

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new LogWorkCommand(_repositoryId, _sprintId, task.Id, 1m, 0m), CancellationToken.None);

        var updated = await LoadAsync(task.Id);
        updated.ClosedAt.Should().BeCloseTo(originalClosedAt, TimeSpan.FromSeconds(1),
            "the auto-close branch is skipped for an item already Done");
        updated.CompletedWork.Should().Be(1m, "extra hours still get logged");
    }
}
