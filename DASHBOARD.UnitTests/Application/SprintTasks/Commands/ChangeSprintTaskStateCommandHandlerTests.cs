using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.SprintTasks.Commands.ChangeSprintTaskState;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.UnitTests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DASHBOARD.UnitTests.Application.SprintTasks.Commands;

/// <summary>Unit tests for <see cref="ChangeSprintTaskStateCommandHandler"/>.</summary>
public sealed class ChangeSprintTaskStateCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Mock<IHistoryService>    _history  = new();
    private readonly Mock<IEmailService>      _email    = new();
    private readonly Mock<IAppSettings>       _settings = new();
    private readonly Guid                     _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public ChangeSprintTaskStateCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;

        _settings.SetupGet(s => s.InvitationFrontendBaseUrl).Returns("https://localhost");
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private ChangeSprintTaskStateCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _history.Object, _database.Uow, _publisher,
            _email.Object, _settings.Object,
            NullLogger<ChangeSprintTaskStateCommandHandler>.Instance);

    private readonly RecordingPublishEndpoint _publisher = new();

    private IRequestUserContext MemberUser() =>
        RequestUserContextMock.ForUser().AsMember(_repositoryId).Object;

    private async Task<SprintTask> AddTaskAsync(
        WorkItemState state         = WorkItemState.Open,
        decimal       remainingWork = 8m,
        DateTime?     closedAt      = null,
        Guid?         repositoryId  = null)
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
            new ChangeSprintTaskStateCommand(_repositoryId, task.Id, WorkItemState.Done),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await LoadAsync(task.Id)).State.Should().Be(WorkItemState.Open);
    }

    [Fact]
    public async Task Handle_ForAnyMember_AllowsTheTransition()
    {
        // This handler gates on membership alone, unlike UpdateSprintTask which requires
        // EditWorkItem. Dragging a card on the board is deliberately open to every member.
        var task    = await AddTaskAsync();
        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new ChangeSprintTaskStateCommand(_repositoryId, task.Id, WorkItemState.InProgress),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // ── Not found ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTaskDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(MemberUser());

        var act = () => handler.Handle(
            new ChangeSprintTaskStateCommand(_repositoryId, Guid.NewGuid(), WorkItemState.Done),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenTaskBelongsToAnotherRepository_ThrowsNotFound()
    {
        var foreign = await AddTaskAsync(repositoryId: Guid.NewGuid());
        var handler = CreateHandler(MemberUser());

        var act = () => handler.Handle(
            new ChangeSprintTaskStateCommand(_repositoryId, foreign.Id, WorkItemState.Done),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Transitions ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(WorkItemState.ToDo)]
    [InlineData(WorkItemState.InProgress)]
    [InlineData(WorkItemState.InReview)]
    public async Task Handle_AppliesTheTargetStateAndStampsStateChangedAt(WorkItemState target)
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
        // There is no transition matrix — the board lets a card jump straight from Open to Done.
        var task    = await AddTaskAsync(WorkItemState.Open);
        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new ChangeSprintTaskStateCommand(_repositoryId, task.Id, WorkItemState.Done),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await LoadAsync(task.Id)).State.Should().Be(WorkItemState.Done);
    }

    // ── Done side effects ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_MovingToDone_ZeroesRemainingWorkAndStampsClosedAt()
    {
        var task    = await AddTaskAsync(remainingWork: 12m);
        var handler = CreateHandler(MemberUser());

        await handler.Handle(
            new ChangeSprintTaskStateCommand(_repositoryId, task.Id, WorkItemState.Done),
            CancellationToken.None);

        var updated = await LoadAsync(task.Id);
        updated.RemainingWork.Should().Be(0m);
        updated.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ReopeningFromDone_ClearsClosedAt()
    {
        var task    = await AddTaskAsync(WorkItemState.Done, remainingWork: 0m,
                                         closedAt: DateTime.UtcNow.AddDays(-1));
        var handler = CreateHandler(MemberUser());

        await handler.Handle(
            new ChangeSprintTaskStateCommand(_repositoryId, task.Id, WorkItemState.InProgress),
            CancellationToken.None);

        var updated = await LoadAsync(task.Id);
        updated.State.Should().Be(WorkItemState.InProgress);
        updated.ClosedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ReopeningDoesNotRestoreTheRemainingWork()
    {
        var task    = await AddTaskAsync(WorkItemState.Done, remainingWork: 0m);
        var handler = CreateHandler(MemberUser());

        await handler.Handle(
            new ChangeSprintTaskStateCommand(_repositoryId, task.Id, WorkItemState.InProgress),
            CancellationToken.None);

        (await LoadAsync(task.Id)).RemainingWork.Should().Be(0m,
            "the original estimate is gone — whoever reopens the item re-enters it");
    }

    // ── History ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_RecordsTheTransition()
    {
        var task    = await AddTaskAsync(WorkItemState.ToDo);
        var handler = CreateHandler(MemberUser());

        await handler.Handle(
            new ChangeSprintTaskStateCommand(_repositoryId, task.Id, WorkItemState.InProgress),
            CancellationToken.None);

        _history.Verify(h => h.Record(task.Id, _repositoryId, It.IsAny<Guid>(),
            "State changed from 'ToDo' to 'InProgress'."), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTheStateIsUnchanged_RecordsNoHistory()
    {
        var task    = await AddTaskAsync(WorkItemState.InProgress);
        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new ChangeSprintTaskStateCommand(_repositoryId, task.Id, WorkItemState.InProgress),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue("a no-op transition is accepted, just not logged");
        _history.Verify(h => h.Record(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<string>()), Times.Never);
    }
}
