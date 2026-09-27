using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.SprintTasks.Commands.UpdateRemainingWork;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Tests.Application.SprintTasks.Commands;

/// <summary>
/// Unit tests for <see cref="UpdateRemainingWorkCommandHandler"/>.
/// </summary>
/// <remarks>
/// This is the inline "remaining hours" edit on a board card. Like <c>LogWork</c> it can close an
/// item by driving remaining work to zero, but unlike <c>LogWork</c> it still gates on membership
/// alone — the tests below pin that as it stands today.
/// </remarks>
public sealed class UpdateRemainingWorkCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();
    private readonly Guid                     _sprintId     = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public UpdateRemainingWorkCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private UpdateRemainingWorkCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private IRequestUserContext MemberUser() =>
        RequestUserContextMock.ForUser().AsMember(_repositoryId).Object;

    private async Task<SprintTask> AddTaskAsync(
        decimal         remainingWork = 8m,
        SprintTaskState state         = SprintTaskState.Active,
        DateTime?       closedAt      = null,
        Guid?           sprintId      = null)
    {
        var task = new SprintTask
        {
            RepositoryId  = _repositoryId,
            SprintId      = sprintId ?? _sprintId,
            Type          = SprintTaskType.Task,
            Title         = "Task",
            State         = state,
            RemainingWork = remainingWork,
            ClosedAt      = closedAt,
        };
        _db.Set<SprintTask>().Add(task);
        await _db.SaveChangesAsync();
        return task;
    }

    private Task<SprintTask> LoadAsync(Guid id) =>
        _db.Set<SprintTask>().AsNoTracking().SingleAsync(t => t.Id == id);

    // ── Access control ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheUserIsNotAMember_FailsAndLeavesTheHoursAlone()
    {
        var task    = await AddTaskAsync(remainingWork: 8m);
        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var result = await handler.Handle(
            new UpdateRemainingWorkCommand(_repositoryId, _sprintId, task.Id, 3m),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not a member");
        (await LoadAsync(task.Id)).RemainingWork.Should().Be(8m);
    }

    // ── Input validation ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithNegativeRemainingWork_Fails()
    {
        var task    = await AddTaskAsync();
        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new UpdateRemainingWorkCommand(_repositoryId, _sprintId, task.Id, -1m),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be negative");
        (await LoadAsync(task.Id)).RemainingWork.Should().Be(8m,
            "the value is rejected outright rather than clamped");
    }

    // ── Not found ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheTaskDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(MemberUser());

        var act = () => handler.Handle(
            new UpdateRemainingWorkCommand(_repositoryId, _sprintId, Guid.NewGuid(), 3m),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenTheTaskBelongsToAnotherSprint_ThrowsNotFound()
    {
        var task    = await AddTaskAsync(sprintId: Guid.NewGuid());
        var handler = CreateHandler(MemberUser());

        var act = () => handler.Handle(
            new UpdateRemainingWorkCommand(_repositoryId, _sprintId, task.Id, 3m),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>(
            "the task is looked up by sprint, not by repository");
    }

    // ── Updating hours ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReplacesTheRemainingHoursAndStampsTheTimestamp()
    {
        var task    = await AddTaskAsync(remainingWork: 8m);
        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new UpdateRemainingWorkCommand(_repositoryId, _sprintId, task.Id, 3m),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = await LoadAsync(task.Id);
        updated.RemainingWork.Should().Be(3m, "the figure is set, not decremented");
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_AcceptsAHigherEstimateThanBefore()
    {
        var task    = await AddTaskAsync(remainingWork: 4m);
        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new UpdateRemainingWorkCommand(_repositoryId, _sprintId, task.Id, 12m),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue("work often turns out larger than first thought");
        (await LoadAsync(task.Id)).RemainingWork.Should().Be(12m);
    }

    [Fact]
    public async Task Handle_DoesNotTouchCompletedWork()
    {
        var task = await AddTaskAsync(remainingWork: 8m);
        task.CompletedWork = 5m;
        await _db.SaveChangesAsync();

        var handler = CreateHandler(MemberUser());

        await handler.Handle(
            new UpdateRemainingWorkCommand(_repositoryId, _sprintId, task.Id, 3m),
            CancellationToken.None);

        (await LoadAsync(task.Id)).CompletedWork.Should().Be(5m,
            "adjusting the estimate is not the same as logging work");
    }

    // ── Auto-close ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenHoursReachZero_ClosesTheTask()
    {
        var task    = await AddTaskAsync(remainingWork: 3m, state: SprintTaskState.Active);
        var handler = CreateHandler(MemberUser());

        await handler.Handle(
            new UpdateRemainingWorkCommand(_repositoryId, _sprintId, task.Id, 0m),
            CancellationToken.None);

        (await LoadAsync(task.Id)).State.Should().Be(SprintTaskState.Done);
    }

    [Fact]
    public async Task Handle_TheAutoCloseDoesNotStampClosedAt()
    {
        // Unlike LogWork and ChangeSprintTaskState, this path sets State without touching
        // ClosedAt — worth pinning so the difference is deliberate rather than forgotten.
        var task    = await AddTaskAsync(remainingWork: 3m, state: SprintTaskState.Active);
        var handler = CreateHandler(MemberUser());

        await handler.Handle(
            new UpdateRemainingWorkCommand(_repositoryId, _sprintId, task.Id, 0m),
            CancellationToken.None);

        (await LoadAsync(task.Id)).ClosedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenHoursStayAboveZero_LeavesTheStateAlone()
    {
        var task    = await AddTaskAsync(remainingWork: 8m, state: SprintTaskState.Active);
        var handler = CreateHandler(MemberUser());

        await handler.Handle(
            new UpdateRemainingWorkCommand(_repositoryId, _sprintId, task.Id, 5m),
            CancellationToken.None);

        (await LoadAsync(task.Id)).State.Should().Be(SprintTaskState.Active);
    }

    [Fact]
    public async Task Handle_SettingZeroOnAnAlreadyDoneTaskKeepsItDone()
    {
        var task    = await AddTaskAsync(remainingWork: 0m, state: SprintTaskState.Done);
        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new UpdateRemainingWorkCommand(_repositoryId, _sprintId, task.Id, 0m),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await LoadAsync(task.Id)).State.Should().Be(SprintTaskState.Done);
    }

    [Fact]
    public async Task Handle_RaisingHoursOnADoneTaskDoesNotReopenIt()
    {
        var task    = await AddTaskAsync(remainingWork: 0m, state: SprintTaskState.Done);
        var handler = CreateHandler(MemberUser());

        await handler.Handle(
            new UpdateRemainingWorkCommand(_repositoryId, _sprintId, task.Id, 5m),
            CancellationToken.None);

        var updated = await LoadAsync(task.Id);
        updated.RemainingWork.Should().Be(5m);
        updated.State.Should().Be(SprintTaskState.Done,
            "reopening is an explicit state change, not a side effect of editing hours");
    }
}
