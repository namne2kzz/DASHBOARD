using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.SmartBoard.Commands.ReorderColumns;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Tests.Application.SmartBoard.Commands;

/// <summary>Unit tests for <see cref="ReorderColumnsCommandHandler"/>.</summary>
public sealed class ReorderColumnsCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public ReorderColumnsCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private ReorderColumnsCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private IRequestUserContext AuthorizedUser() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.ManageBoard, _repositoryId).Object;

    private async Task<SmartBoardColumn> AddColumnAsync(
        string name, int order, Guid? repositoryId = null)
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

    private async Task<List<string>> ColumnNamesInOrderAsync() =>
        await _db.Set<SmartBoardColumn>().AsNoTracking()
            .Where(c => c.RepositoryId == _repositoryId)
            .OrderBy(c => c.Order)
            .Select(c => c.Name)
            .ToListAsync();

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutManageBoardPrivilege_FailsAndKeepsTheOrder()
    {
        var first  = await AddColumnAsync("First",  0);
        var second = await AddColumnAsync("Second", 1);

        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var result = await handler.Handle(
            new ReorderColumnsCommand(_repositoryId, [second.Id, first.Id]), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("permission");
        (await ColumnNamesInOrderAsync()).Should().ContainInOrder("First", "Second");
    }

    // ── Completeness validation ─────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenAColumnIsMissingFromTheList_Fails()
    {
        var first = await AddColumnAsync("First", 0);
        await AddColumnAsync("Second", 1);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new ReorderColumnsCommand(_repositoryId, [first.Id]), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("every column exactly once");
    }

    [Fact]
    public async Task Handle_WhenTheListHasAnExtraId_Fails()
    {
        var first  = await AddColumnAsync("First",  0);
        var second = await AddColumnAsync("Second", 1);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new ReorderColumnsCommand(_repositoryId, [first.Id, second.Id, Guid.NewGuid()]),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenAnIdIsRepeated_Fails()
    {
        var first = await AddColumnAsync("First", 0);
        await AddColumnAsync("Second", 1);

        var handler = CreateHandler(AuthorizedUser());

        // Right count, wrong content — a duplicate would leave one column unordered.
        var result = await handler.Handle(
            new ReorderColumnsCommand(_repositoryId, [first.Id, first.Id]), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("exactly once");
    }

    [Fact]
    public async Task Handle_WhenTheListNamesAColumnOfAnotherRepository_Fails()
    {
        var first   = await AddColumnAsync("First", 0);
        var foreign = await AddColumnAsync("Foreign", 0, repositoryId: Guid.NewGuid());

        var handler = CreateHandler(AuthorizedUser());

        // Correct count, but one id belongs to a board this caller is not reordering.
        var result = await handler.Handle(
            new ReorderColumnsCommand(_repositoryId, [first.Id, foreign.Id]), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithNoColumnsAndAnEmptyList_Succeeds()
    {
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new ReorderColumnsCommand(_repositoryId, []), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("an empty board is trivially in order");
    }

    // ── Reordering ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AppliesTheSubmittedOrder()
    {
        var first  = await AddColumnAsync("First",  0);
        var second = await AddColumnAsync("Second", 1);
        var third  = await AddColumnAsync("Third",  2);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new ReorderColumnsCommand(_repositoryId, [third.Id, first.Id, second.Id]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await ColumnNamesInOrderAsync()).Should().ContainInOrder("Third", "First", "Second");
    }

    [Fact]
    public async Task Handle_NumbersTheOrderFromZeroWithoutGaps()
    {
        var first  = await AddColumnAsync("First",  10);
        var second = await AddColumnAsync("Second", 25);

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new ReorderColumnsCommand(_repositoryId, [second.Id, first.Id]), CancellationToken.None);

        var orders = await _db.Set<SmartBoardColumn>().AsNoTracking()
            .Where(c => c.RepositoryId == _repositoryId)
            .OrderBy(c => c.Order)
            .Select(c => c.Order)
            .ToListAsync();

        orders.Should().Equal([0, 1], "the submitted list index becomes the order");
    }

    [Fact]
    public async Task Handle_StampsTheUpdatedTimestamp()
    {
        var first  = await AddColumnAsync("First",  0);
        var second = await AddColumnAsync("Second", 1);

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new ReorderColumnsCommand(_repositoryId, [second.Id, first.Id]), CancellationToken.None);

        var columns = await _db.Set<SmartBoardColumn>().AsNoTracking()
            .Where(c => c.RepositoryId == _repositoryId)
            .ToListAsync();

        columns.Should().OnlyContain(c => c.UpdatedAt != null);
    }

    [Fact]
    public async Task Handle_DoesNotTouchAnotherRepositorysBoard()
    {
        var first   = await AddColumnAsync("First",  0);
        var second  = await AddColumnAsync("Second", 1);
        var foreign = await AddColumnAsync("Foreign", 7, repositoryId: Guid.NewGuid());

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new ReorderColumnsCommand(_repositoryId, [second.Id, first.Id]), CancellationToken.None);

        var untouched = await _db.Set<SmartBoardColumn>().AsNoTracking()
            .SingleAsync(c => c.Id == foreign.Id);
        untouched.Order.Should().Be(7);
    }
}
