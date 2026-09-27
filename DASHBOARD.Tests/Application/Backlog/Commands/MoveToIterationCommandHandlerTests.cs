using DASHBOARD.Application.Backlog.Commands.MoveToIteration;
using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Tests.Application.Backlog.Commands;

/// <summary>Unit tests for <see cref="MoveToIterationCommandHandler"/>.</summary>
public sealed class MoveToIterationCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();
    private readonly Guid                     _sprintId     = Guid.NewGuid();

    /// <summary>Sets up an isolated database holding one sprint.</summary>
    public MoveToIterationCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;

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

    private MoveToIterationCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private IRequestUserContext AuthorizedUser() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.ManageBacklog, _repositoryId).Object;

    private async Task<BacklogItem> AddItemAsync(
        Guid?            sprintId     = null,
        BacklogItemState state        = BacklogItemState.New,
        Guid?            repositoryId = null)
    {
        var item = new BacklogItem
        {
            RepositoryId = repositoryId ?? _repositoryId,
            Type         = BacklogItemType.UserStory,
            State        = state,
            Title        = "Item",
            Rank         = 1000m,
            SprintId     = sprintId,
        };
        _db.Set<BacklogItem>().Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    private Task<BacklogItem> LoadAsync(Guid id) =>
        _db.Set<BacklogItem>().AsNoTracking().SingleAsync(b => b.Id == id);

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutManageBacklogPrivilege_FailsAndLeavesTheItemUnchanged()
    {
        var item    = await AddItemAsync();
        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var result = await handler.Handle(
            new MoveToIterationCommand(_repositoryId, item.Id, _sprintId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("permission");
        (await LoadAsync(item.Id)).SprintId.Should().BeNull();
    }

    // ── Sprint validation ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSprintDoesNotExist_Fails()
    {
        var item    = await AddItemAsync();
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new MoveToIterationCommand(_repositoryId, item.Id, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Sprint not found");
    }

    [Fact]
    public async Task Handle_WhenSprintBelongsToAnotherRepository_Fails()
    {
        var foreignSprintId = Guid.NewGuid();
        _db.Set<Sprint>().Add(new Sprint
        {
            RepositoryId = Guid.NewGuid(),
            Name         = "Foreign",
            StartDate    = new DateOnly(2026, 6, 1),
            EndDate      = new DateOnly(2026, 6, 12),
        }.WithId(foreignSprintId));
        await _db.SaveChangesAsync();

        var item    = await AddItemAsync();
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new MoveToIterationCommand(_repositoryId, item.Id, foreignSprintId), CancellationToken.None);

        result.IsFailure.Should().BeTrue("a backlog item may only be scheduled into its own repository's sprints");
    }

    // ── Item validation ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenItemDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new MoveToIterationCommand(_repositoryId, Guid.NewGuid(), _sprintId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenItemBelongsToAnotherRepository_ThrowsNotFound()
    {
        var foreign = await AddItemAsync(repositoryId: Guid.NewGuid());
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new MoveToIterationCommand(_repositoryId, foreign.Id, _sprintId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Assignment ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AssignsTheItemToTheSprint()
    {
        var item    = await AddItemAsync();
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new MoveToIterationCommand(_repositoryId, item.Id, _sprintId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = await LoadAsync(item.Id);
        updated.SprintId.Should().Be(_sprintId);
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WithANullSprint_ClearsTheAssignment()
    {
        var item    = await AddItemAsync(sprintId: _sprintId);
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new MoveToIterationCommand(_repositoryId, item.Id, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await LoadAsync(item.Id)).SprintId.Should().BeNull("the item returns to the unscheduled backlog");
    }

    [Fact]
    public async Task Handle_DoesNotChangeTheRefinementState()
    {
        var item    = await AddItemAsync(state: BacklogItemState.Refining);
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new MoveToIterationCommand(_repositoryId, item.Id, _sprintId), CancellationToken.None);

        (await LoadAsync(item.Id)).State.Should().Be(BacklogItemState.Refining,
            "scheduling is planning, not refinement — only PromoteToSprint changes state");
    }

    [Fact]
    public async Task Handle_CanMoveAnItemBetweenSprints()
    {
        var secondSprintId = Guid.NewGuid();
        _db.Set<Sprint>().Add(new Sprint
        {
            RepositoryId = _repositoryId,
            Name         = "Sprint 2",
            StartDate    = new DateOnly(2026, 6, 1),
            EndDate      = new DateOnly(2026, 6, 12),
        }.WithId(secondSprintId));
        await _db.SaveChangesAsync();

        var item    = await AddItemAsync(sprintId: _sprintId);
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new MoveToIterationCommand(_repositoryId, item.Id, secondSprintId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await LoadAsync(item.Id)).SprintId.Should().Be(secondSprintId);
    }
}
