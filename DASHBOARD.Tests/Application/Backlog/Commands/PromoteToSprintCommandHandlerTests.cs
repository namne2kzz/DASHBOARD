using DASHBOARD.Application.Backlog.Commands.PromoteToSprint;
using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DASHBOARD.Tests.Application.Backlog.Commands;

/// <summary>
/// Unit tests for <see cref="PromoteToSprintCommandHandler"/>.
/// </summary>
/// <remarks>
/// BUG-009 (High) covered this handler: three business-rule paths threw instead of returning a
/// result, which broke a debugger on every ordinary "wrong state" attempt. The permission,
/// wrong-type and wrong-state tests assert <c>Result.Failure</c> rather than an exception.
/// </remarks>
public sealed class PromoteToSprintCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Mock<IHistoryService>    _history = new();
    private readonly Guid                     _repositoryId = Guid.NewGuid();
    private readonly Guid                     _sprintId     = Guid.NewGuid();

    /// <summary>Sets up an isolated database holding one sprint.</summary>
    public PromoteToSprintCommandHandlerTests()
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

    private PromoteToSprintCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _history.Object, _database.Uow);

    private IRequestUserContext AuthorizedUser() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.PromoteToSprint, _repositoryId).Object;

    private async Task<BacklogItem> AddItemAsync(
        BacklogItemType  type  = BacklogItemType.UserStory,
        BacklogItemState state = BacklogItemState.Ready,
        string           title = "As a user I want…",
        int?             storyPoints = 5,
        string           acceptanceCriteria = "Given…When…Then…",
        Guid?            repositoryId = null)
    {
        var item = new BacklogItem
        {
            RepositoryId       = repositoryId ?? _repositoryId,
            Type               = type,
            State              = state,
            Title              = title,
            StoryPoints        = storyPoints,
            AcceptanceCriteria = acceptanceCriteria,
            Documents          = ["spec.md"],
        };
        _db.Set<BacklogItem>().Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutPromotePrivilege_FailsWithoutThrowing()
    {
        var item    = await AddItemAsync();
        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var result = await handler.Handle(
            new PromoteToSprintCommand(_repositoryId, item.Id, _sprintId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("permission");
        (await _db.Set<SprintTask>().CountAsync()).Should().Be(0);
    }

    // ── Existence ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenItemDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new PromoteToSprintCommand(_repositoryId, Guid.NewGuid(), _sprintId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenItemBelongsToAnotherRepository_ThrowsNotFound()
    {
        var foreign = await AddItemAsync(repositoryId: Guid.NewGuid());
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new PromoteToSprintCommand(_repositoryId, foreign.Id, _sprintId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenSprintDoesNotExist_ThrowsNotFound()
    {
        var item    = await AddItemAsync();
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new PromoteToSprintCommand(_repositoryId, item.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Type rule ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(BacklogItemType.Epic)]
    [InlineData(BacklogItemType.Feature)]
    public async Task Handle_WhenItemIsNotAUserStory_FailsWithoutThrowing(BacklogItemType type)
    {
        var item    = await AddItemAsync(type: type);
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new PromoteToSprintCommand(_repositoryId, item.Id, _sprintId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Only UserStory");
        (await _db.Set<SprintTask>().CountAsync()).Should().Be(0);
    }

    // ── State rule ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(BacklogItemState.New)]
    [InlineData(BacklogItemState.Refining)]
    [InlineData(BacklogItemState.Committed)]
    public async Task Handle_WhenItemIsNotReady_FailsWithoutThrowing(BacklogItemState state)
    {
        var item    = await AddItemAsync(state: state);
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new PromoteToSprintCommand(_repositoryId, item.Id, _sprintId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("'Ready' state");
        result.Error.Should().Contain(state.ToString(), "the message names the current state");
    }

    // ── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CreatesASprintTaskCarryingTheBacklogContent()
    {
        var item    = await AddItemAsync(title: "Checkout flow", storyPoints: 8);
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new PromoteToSprintCommand(_repositoryId, item.Id, _sprintId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var task = await _db.Set<SprintTask>().AsNoTracking().SingleAsync();
        task.Id.Should().Be(result.Value);
        task.SprintId.Should().Be(_sprintId);
        task.BacklogItemId.Should().Be(item.Id);
        task.Title.Should().Be("Checkout flow");
        task.Type.Should().Be(SprintTaskType.UserStory);
        task.State.Should().Be(SprintTaskState.New);
        task.StoryPoints.Should().Be(8);
        task.AcceptanceCriteria.Should().Be("Given…When…Then…");
        task.Documents.Should().BeEquivalentTo(["spec.md"]);
        task.StateChangedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_MarksTheBacklogItemCommittedAndLinksTheSprint()
    {
        var item    = await AddItemAsync();
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new PromoteToSprintCommand(_repositoryId, item.Id, _sprintId), CancellationToken.None);

        var updated = await _db.Set<BacklogItem>().AsNoTracking().SingleAsync(b => b.Id == item.Id);
        updated.State.Should().Be(BacklogItemState.Committed);
        updated.SprintId.Should().Be(_sprintId);
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenStoryPointsAreUnset_DefaultsToZero()
    {
        var item    = await AddItemAsync(storyPoints: null);
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new PromoteToSprintCommand(_repositoryId, item.Id, _sprintId), CancellationToken.None);

        (await _db.Set<SprintTask>().AsNoTracking().SingleAsync()).StoryPoints.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WhenAcceptanceCriteriaIsBlank_StoresNull(string criteria)
    {
        var item    = await AddItemAsync(acceptanceCriteria: criteria);
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new PromoteToSprintCommand(_repositoryId, item.Id, _sprintId), CancellationToken.None);

        (await _db.Set<SprintTask>().AsNoTracking().SingleAsync()).AcceptanceCriteria.Should().BeNull();
    }

    // ── Work-item numbering ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ForTheFirstWorkItem_NumbersItOne()
    {
        var item    = await AddItemAsync();
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new PromoteToSprintCommand(_repositoryId, item.Id, _sprintId), CancellationToken.None);

        (await _db.Set<SprintTask>().AsNoTracking().SingleAsync()).WorkItemNumber.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ContinuesTheWorkItemNumberingOfTheRepository()
    {
        _db.Set<SprintTask>().Add(new SprintTask
        {
            RepositoryId   = _repositoryId,
            SprintId       = _sprintId,
            Title          = "Existing",
            WorkItemNumber = 42,
        });
        await _db.SaveChangesAsync();

        var item    = await AddItemAsync();
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new PromoteToSprintCommand(_repositoryId, item.Id, _sprintId), CancellationToken.None);

        var created = await _db.Set<SprintTask>().AsNoTracking().SingleAsync(t => t.Id == result.Value);
        created.WorkItemNumber.Should().Be(43);
    }

    [Fact]
    public async Task Handle_NumbersPerRepositoryNotGlobally()
    {
        _db.Set<SprintTask>().Add(new SprintTask
        {
            RepositoryId   = Guid.NewGuid(),
            Title          = "Foreign",
            WorkItemNumber = 99,
        });
        await _db.SaveChangesAsync();

        var item    = await AddItemAsync();
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new PromoteToSprintCommand(_repositoryId, item.Id, _sprintId), CancellationToken.None);

        var created = await _db.Set<SprintTask>().AsNoTracking().SingleAsync(t => t.Id == result.Value);
        created.WorkItemNumber.Should().Be(1, "another repository's numbering must not leak in");
    }

    // ── History ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_RecordsCreationHistory()
    {
        var item    = await AddItemAsync(title: "Checkout flow", storyPoints: 8);
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new PromoteToSprintCommand(_repositoryId, item.Id, _sprintId), CancellationToken.None);

        _history.Verify(h => h.Record(result.Value, _repositoryId, It.IsAny<Guid>(),
            "Created this User Story work item."), Times.Once);
        _history.Verify(h => h.Record(result.Value, _repositoryId, It.IsAny<Guid>(),
            "Title: 'Checkout flow'."), Times.Once);
        _history.Verify(h => h.Record(result.Value, _repositoryId, It.IsAny<Guid>(),
            "Story points: 8."), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenStoryPointsAreZero_SkipsTheStoryPointsHistoryLine()
    {
        var item    = await AddItemAsync(storyPoints: 0);
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new PromoteToSprintCommand(_repositoryId, item.Id, _sprintId), CancellationToken.None);

        _history.Verify(h => h.Record(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.Is<string>(m => m.StartsWith("Story points"))), Times.Never);
    }
}
