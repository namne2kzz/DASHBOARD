using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.SmartBoard.Commands.CreateColumn;
using DASHBOARD.Application.SmartBoard.Commands.UpdateColumn;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.UnitTests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.UnitTests.Application.SmartBoard.Commands;

/// <summary>Unit tests for <see cref="CreateColumnCommandHandler"/> and <see cref="UpdateColumnCommandHandler"/>.</summary>
public sealed class ColumnCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public ColumnCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private CreateColumnCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private UpdateColumnCommandHandler UpdateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private IRequestUserContext AuthorizedUser() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.ManageBoard, _repositoryId).Object;

    private CreateColumnCommand CreateCommand(
        string          name           = "In Progress",
        SprintTaskState mappedState    = SprintTaskState.Active,
        int             wipLimit       = 5,
        WipMode         wipMode        = WipMode.Soft,
        int             agingLimitDays = 3) =>
        new(_repositoryId, name, mappedState, wipLimit, wipMode, agingLimitDays);

    private async Task<SmartBoardColumn> AddColumnAsync(
        string name = "Todo", int order = 0, Guid? repositoryId = null)
    {
        var column = new SmartBoardColumn
        {
            RepositoryId   = repositoryId ?? _repositoryId,
            Name           = name,
            MappedState    = SprintTaskState.Todo,
            WipLimit       = 5,
            WipMode        = WipMode.Soft,
            AgingLimitDays = 3,
            Order          = order,
        };
        _db.Set<SmartBoardColumn>().Add(column);
        await _db.SaveChangesAsync();
        return column;
    }

    // ── Create: permission ──────────────────────────────────────────────────

    [Fact]
    public async Task Create_WithoutManageBoardPrivilege_Throws()
    {
        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(CreateCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await _db.Set<SmartBoardColumn>().CountAsync()).Should().Be(0);
    }

    // ── Create: ordering ────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ForTheFirstColumn_StartsOrderAtZero()
    {
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(CreateCommand(), CancellationToken.None);

        result.Order.Should().Be(0);
    }

    [Fact]
    public async Task Create_AppendsAfterTheLastColumn()
    {
        await AddColumnAsync("Todo",   0);
        await AddColumnAsync("Active", 4);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(CreateCommand(), CancellationToken.None);

        result.Order.Should().Be(5, "a new column goes to the end of the board");
    }

    [Fact]
    public async Task Create_OrderingIsScopedToTheRepository()
    {
        await AddColumnAsync("Foreign", 9, repositoryId: Guid.NewGuid());

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(CreateCommand(), CancellationToken.None);

        result.Order.Should().Be(0, "another board's ordering must not leak in");
    }

    // ── Create: persistence ─────────────────────────────────────────────────

    [Fact]
    public async Task Create_PersistsTheConfiguration()
    {
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            CreateCommand("Review", SprintTaskState.InReview, wipLimit: 2,
                wipMode: WipMode.Hard, agingLimitDays: 7),
            CancellationToken.None);

        var persisted = await _db.Set<SmartBoardColumn>().AsNoTracking().SingleAsync();
        persisted.Id.Should().Be(result.Id);
        persisted.RepositoryId.Should().Be(_repositoryId);
        persisted.Name.Should().Be("Review");
        persisted.MappedState.Should().Be(SprintTaskState.InReview);
        persisted.WipLimit.Should().Be(2);
        persisted.WipMode.Should().Be(WipMode.Hard);
        persisted.AgingLimitDays.Should().Be(7);
    }

    [Fact]
    public async Task Create_AllowsTwoColumnsMappedToTheSameState()
    {
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(CreateCommand("Dev Active", SprintTaskState.Active), CancellationToken.None);
        var second = await handler.Handle(
            CreateCommand("QA Active", SprintTaskState.Active), CancellationToken.None);

        second.Order.Should().Be(1,
            "splitting one state across lanes is a normal board layout");
        (await _db.Set<SmartBoardColumn>().CountAsync()).Should().Be(2);
    }

    // ── Update: permission and existence ────────────────────────────────────

    [Fact]
    public async Task Update_WithoutManageBoardPrivilege_FailsAndKeepsTheConfiguration()
    {
        var column  = await AddColumnAsync("Todo");
        var handler = UpdateHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(
            new UpdateColumnCommand(_repositoryId, column.Id, "Renamed",
                SprintTaskState.Done, 99, WipMode.Hard, 30),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();

        var unchanged = await _db.Set<SmartBoardColumn>().AsNoTracking().SingleAsync();
        unchanged.Name.Should().Be("Todo");
    }

    [Fact]
    public async Task Update_WhenTheColumnDoesNotExist_ThrowsNotFound()
    {
        var handler = UpdateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new UpdateColumnCommand(_repositoryId, Guid.NewGuid(), "Renamed",
                SprintTaskState.Done, 5, WipMode.Soft, 3),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Update_WhenTheColumnBelongsToAnotherRepository_ThrowsNotFound()
    {
        var foreign = await AddColumnAsync("Foreign", repositoryId: Guid.NewGuid());
        var handler = UpdateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new UpdateColumnCommand(_repositoryId, foreign.Id, "Renamed",
                SprintTaskState.Done, 5, WipMode.Soft, 3),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Update: happy path ──────────────────────────────────────────────────

    [Fact]
    public async Task Update_AppliesEveryConfigurationFieldAndTimestamp()
    {
        var column  = await AddColumnAsync("Todo");
        var handler = UpdateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new UpdateColumnCommand(_repositoryId, column.Id, "In Review",
                SprintTaskState.InReview, 2, WipMode.Hard, 7),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = await _db.Set<SmartBoardColumn>().AsNoTracking().SingleAsync();
        updated.Name.Should().Be("In Review");
        updated.MappedState.Should().Be(SprintTaskState.InReview);
        updated.WipLimit.Should().Be(2);
        updated.WipMode.Should().Be(WipMode.Hard);
        updated.AgingLimitDays.Should().Be(7);
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Update_DoesNotChangeTheColumnOrder()
    {
        var column  = await AddColumnAsync("Todo", order: 3);
        var handler = UpdateHandler(AuthorizedUser());

        await handler.Handle(
            new UpdateColumnCommand(_repositoryId, column.Id, "Renamed",
                SprintTaskState.Done, 5, WipMode.Soft, 3),
            CancellationToken.None);

        var updated = await _db.Set<SmartBoardColumn>().AsNoTracking().SingleAsync();
        updated.Order.Should().Be(3, "position is owned by ReorderColumns, not by an edit");
    }
}
