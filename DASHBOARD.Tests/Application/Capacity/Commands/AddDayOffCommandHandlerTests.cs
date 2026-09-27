using DASHBOARD.Application.Capacity.Commands.AddDayOff;
using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Tests.Application.Capacity.Commands;

/// <summary>Unit tests for <see cref="AddDayOffCommandHandler"/>.</summary>
public sealed class AddDayOffCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();
    private readonly Guid                     _sprintId     = Guid.NewGuid();

    /// <summary>Sets up an isolated database holding a sprint spanning Mon 4 May → Fri 15 May 2026.</summary>
    public AddDayOffCommandHandlerTests()
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

    private AddDayOffCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private IRequestUserContext AuthorizedUser() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.ManageCapacity, _repositoryId).Object;

    private AddDayOffCommand Command(
        Guid?     userId = null,
        DateOnly? date   = null,
        decimal   hours  = 8m,
        string    reason = "Public holiday",
        Guid?     sprintId = null) =>
        new(_repositoryId, sprintId ?? _sprintId, userId,
            date ?? new DateOnly(2026, 5, 6), hours, reason);

    private async Task<User> AddUserAsync(string name = "Alice")
    {
        var user = new User
        {
            Name         = name,
            Email        = $"{name.ToLowerInvariant()}@test.local",
            PasswordHash = "hash",
            PasswordSalt = "salt",
        };
        _db.Set<User>().Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutManageCapacityPrivilege_FailsWithoutCreatingAnything()
    {
        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("permission");
        (await _db.Set<DayOff>().CountAsync()).Should().Be(0);
    }

    // ── Sprint resolution ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSprintDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            Command(sprintId: Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenSprintBelongsToAnotherRepository_ThrowsNotFound()
    {
        var foreignSprintId = Guid.NewGuid();
        _db.Set<Sprint>().Add(new Sprint
        {
            RepositoryId = Guid.NewGuid(),
            Name         = "Foreign",
            StartDate    = new DateOnly(2026, 5, 4),
            EndDate      = new DateOnly(2026, 5, 15),
        }.WithId(foreignSprintId));
        await _db.SaveChangesAsync();

        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            Command(sprintId: foreignSprintId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Date range rule ─────────────────────────────────────────────────────

    [Theory]
    // the day before the sprint starts
    [InlineData("2026-05-03")]
    // the day after it ends
    [InlineData("2026-05-16")]
    // far outside
    [InlineData("2026-01-01")]
    public async Task Handle_WhenTheDateIsOutsideTheSprint_Fails(string date)
    {
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(date: DateOnly.Parse(date)), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("outside the sprint range");
        (await _db.Set<DayOff>().CountAsync()).Should().Be(0);
    }

    [Theory]
    // first day of the sprint
    [InlineData("2026-05-04")]
    // last day of the sprint
    [InlineData("2026-05-15")]
    // a weekend inside the range
    [InlineData("2026-05-09")]
    public async Task Handle_WhenTheDateIsWithinTheSprint_Succeeds(string date)
    {
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(date: DateOnly.Parse(date)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("both boundary dates are inclusive");
    }

    // ── Team-wide vs personal ───────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithNoUserId_CreatesATeamWideEntry()
    {
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(Command(userId: null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.UserId.Should().BeNull();
        result.Value.UserName.Should().BeNull();

        var persisted = await _db.Set<DayOff>().AsNoTracking().SingleAsync();
        persisted.UserId.Should().BeNull("a null UserId marks the whole team");
    }

    [Fact]
    public async Task Handle_WithAUserId_EchoesTheUserName()
    {
        var alice   = await AddUserAsync();
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(Command(userId: alice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.UserId.Should().Be(alice.Id);
        result.Value.UserName.Should().Be("Alice");
    }

    [Fact]
    public async Task Handle_WhenTheNamedUserDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(Command(userId: Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Persistence ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_PersistsTheEntryAgainstTheSprintAndRepository()
    {
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(date: new DateOnly(2026, 5, 6), hours: 4m, reason: "Half day"),
            CancellationToken.None);

        var persisted = await _db.Set<DayOff>().AsNoTracking().SingleAsync();
        persisted.Id.Should().Be(result.Value!.Id);
        persisted.SprintId.Should().Be(_sprintId);
        persisted.RepositoryId.Should().Be(_repositoryId);
        persisted.Date.Should().Be(new DateOnly(2026, 5, 6));
        persisted.Hours.Should().Be(4m);
        persisted.Reason.Should().Be("Half day");
    }

    [Fact]
    public async Task Handle_AllowsSeveralEntriesOnTheSameDate()
    {
        var alice   = await AddUserAsync("Alice");
        var bob     = await AddUserAsync("Bob");
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(Command(userId: alice.Id), CancellationToken.None);
        var second = await handler.Handle(Command(userId: bob.Id), CancellationToken.None);

        second.IsSuccess.Should().BeTrue("two people can be away on the same day");
        (await _db.Set<DayOff>().CountAsync()).Should().Be(2);
    }
}
