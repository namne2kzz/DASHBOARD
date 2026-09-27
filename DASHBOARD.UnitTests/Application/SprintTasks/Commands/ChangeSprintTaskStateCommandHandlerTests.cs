using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.SprintTasks.Commands.ChangeSprintTaskState;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.UnitTests.Common;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DASHBOARD.UnitTests.Application.SprintTasks.Commands;

/// <summary>Unit tests for <see cref="ChangeSprintTaskStateCommandHandler"/>.</summary>
public sealed class ChangeSprintTaskStateCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Mock<IHistoryService>    _history = new();
    private readonly Guid                     _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public ChangeSprintTaskStateCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private ChangeSprintTaskStateCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _history.Object, _database.Uow);

    private IRequestUserContext MemberUser() =>
        RequestUserContextMock.ForUser().AsMember(_repositoryId).Object;

    private async Task<SprintTask> AddTaskAsync(
        SprintTaskState state         = SprintTaskState.New,
        decimal         remainingWork = 8m,
        DateTime?       closedAt      = null,
        Guid?           repositoryId  = null)
    {
        var task = new SprintTask
        {
            RepositoryId  = repositoryId ?? _repositoryId,
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
    public async Task Handle_WhenUserIsNotAMember_FailsAndLeavesTheStateUnchanged()
    {
        var task    = await AddTaskAsync();
        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(
            new ChangeSprintTaskStateCommand(_repositoryId, task.Id, SprintTaskState.Done),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await LoadAsync(task.Id)).State.Should().Be(SprintTaskState.New);
    }

    [Fact]
    public async Task Handle_ForAnyMember_AllowsTheTransition()
    {
        // This handler gates on membership alone, unlike UpdateSprintTask which requires
        // EditWorkItem. Dragging a card on the board is deliberately open to every member.
        var task    = await AddTaskAsync();
        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new ChangeSprintTaskStateCommand(_repositoryId, task.Id, SprintTaskState.Active),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // ── Not found ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTaskDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(MemberUser());

        var act = () => handler.Handle(
            new ChangeSprintTaskStateCommand(_repositoryId, Guid.NewGuid(), SprintTaskState.Done),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenTaskBelongsToAnotherRepository_ThrowsNotFound()
    {
        var foreign = await AddTaskAsync(repositoryId: Guid.NewGuid());
        var handler = CreateHandler(MemberUser());

        var act = () => handler.Handle(
            new ChangeSprintTaskStateCommand(_repositoryId, foreign.Id, SprintTaskState.Done),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Transitions ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(SprintTaskState.Backlog)]
    [InlineData(SprintTaskState.Todo)]
    [InlineData(SprintTaskState.Active)]
    [InlineData(SprintTaskState.InReview)]
    public async Task Handle_AppliesTheTargetStateAndStampsStateChangedAt(SprintTaskState target)
    {
        var task    = await AddTaskAsync();
        var handler = CreateHandler(MemberUser());

        await handler.Handle(
            new ChangeSprintTaskStateCommand(_repositoryId, task.Id, target), CancellationToken.None);

        var updated = await LoadAsync(task.Id);
        updated.State.Should().Be(target);
        updated.StateChangedAt.Should().NotBeNull();
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_AllowsAnyTransitionIncludingSkippingStates()
    {
        // There is no transition matrix — the board lets a card jump straight from New to Done.
        var task    = await AddTaskAsync(SprintTaskState.New);
        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new ChangeSprintTaskStateCommand(_repositoryId, task.Id, SprintTaskState.Done),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await LoadAsync(task.Id)).State.Should().Be(SprintTaskState.Done);
    }

    // ── Done side effects ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_MovingToDone_ZeroesRemainingWorkAndStampsClosedAt()
    {
        var task    = await AddTaskAsync(remainingWork: 12m);
        var handler = CreateHandler(MemberUser());

        await handler.Handle(
            new ChangeSprintTaskStateCommand(_repositoryId, task.Id, SprintTaskState.Done),
            CancellationToken.None);

        var updated = await LoadAsync(task.Id);
        updated.RemainingWork.Should().Be(0m);
        updated.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ReopeningFromDone_ClearsClosedAt()
    {
        var task    = await AddTaskAsync(SprintTaskState.Done, remainingWork: 0m,
                                         closedAt: DateTime.UtcNow.AddDays(-1));
        var handler = CreateHandler(MemberUser());

        await handler.Handle(
            new ChangeSprintTaskStateCommand(_repositoryId, task.Id, SprintTaskState.Active),
            CancellationToken.None);

        var updated = await LoadAsync(task.Id);
        updated.State.Should().Be(SprintTaskState.Active);
        updated.ClosedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ReopeningDoesNotRestoreTheRemainingWork()
    {
        var task    = await AddTaskAsync(SprintTaskState.Done, remainingWork: 0m);
        var handler = CreateHandler(MemberUser());

        await handler.Handle(
            new ChangeSprintTaskStateCommand(_repositoryId, task.Id, SprintTaskState.Active),
            CancellationToken.None);

        (await LoadAsync(task.Id)).RemainingWork.Should().Be(0m,
            "the original estimate is gone — whoever reopens the item re-enters it");
    }

    // ── History ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_RecordsTheTransition()
    {
        var task    = await AddTaskAsync(SprintTaskState.Todo);
        var handler = CreateHandler(MemberUser());

        await handler.Handle(
            new ChangeSprintTaskStateCommand(_repositoryId, task.Id, SprintTaskState.Active),
            CancellationToken.None);

        _history.Verify(h => h.Record(task.Id, _repositoryId, It.IsAny<Guid>(),
            "State changed from 'Todo' to 'Active'."), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTheStateIsUnchanged_RecordsNoHistory()
    {
        var task    = await AddTaskAsync(SprintTaskState.Active);
        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new ChangeSprintTaskStateCommand(_repositoryId, task.Id, SprintTaskState.Active),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue("a no-op transition is accepted, just not logged");
        _history.Verify(h => h.Record(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<string>()), Times.Never);
    }
}
