using DASHBOARD.Application.Capacity.Commands.RemoveDayOff;
using DASHBOARD.Application.Capacity.Queries.GetCapacity;
using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Tests.Application.Capacity;

/// <summary>
/// Unit tests for <see cref="GetCapacityQueryHandler"/> and <see cref="RemoveDayOffCommandHandler"/>.
/// </summary>
public sealed class CapacityReadAndRemovalTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();
    private readonly Guid                     _sprintId     = Guid.NewGuid();

    /// <summary>Sets up an isolated database holding one sprint.</summary>
    public CapacityReadAndRemovalTests()
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

    private GetCapacityQueryHandler ReadHandler(IRequestUserContext user) => new(_db, user);

    private RemoveDayOffCommandHandler RemoveHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private IRequestUserContext MemberUser() =>
        RequestUserContextMock.ForUser().AsMember(_repositoryId).Object;

    private IRequestUserContext CapacityManager() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.ManageCapacity, _repositoryId).Object;

    private async Task<User> AddUserAsync(string name)
    {
        var user = new User
        {
            Name         = name,
            Email        = $"{name.ToLowerInvariant()}@test.local",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            AvatarClass  = "bg-sky-600",
        };
        _db.Set<User>().Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task AddCapacityAsync(Guid userId, Guid? sprintId = null)
    {
        _db.Set<CapacityMember>().Add(new CapacityMember
        {
            SprintId     = sprintId ?? _sprintId,
            RepositoryId = _repositoryId,
            UserId       = userId,
            Role         = "Developer",
            HoursPerDay  = 8m,
        });
        await _db.SaveChangesAsync();
    }

    private async Task<DayOff> AddDayOffAsync(
        Guid? userId = null, DateOnly? date = null, Guid? sprintId = null)
    {
        var dayOff = new DayOff
        {
            SprintId     = sprintId ?? _sprintId,
            RepositoryId = _repositoryId,
            UserId       = userId,
            Date         = date ?? new DateOnly(2026, 5, 6),
            Hours        = 8m,
            Reason       = "Leave",
        };
        _db.Set<DayOff>().Add(dayOff);
        await _db.SaveChangesAsync();
        return dayOff;
    }

    // ── GetCapacity: access control ─────────────────────────────────────────

    [Fact]
    public async Task GetCapacity_WhenUserIsNotAMember_Throws()
    {
        var handler = ReadHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(
            new GetCapacityQuery(_repositoryId, _sprintId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetCapacity_WithNothingConfigured_ReturnsEmptyCollections()
    {
        var handler = ReadHandler(MemberUser());

        var result = await handler.Handle(
            new GetCapacityQuery(_repositoryId, _sprintId), CancellationToken.None);

        result.Members.Should().BeEmpty();
        result.DaysOff.Should().BeEmpty();
    }

    // ── GetCapacity: members ────────────────────────────────────────────────

    [Fact]
    public async Task GetCapacity_ReturnsMembersOrderedByName()
    {
        var zach  = await AddUserAsync("Zach");
        var alice = await AddUserAsync("Alice");
        await AddCapacityAsync(zach.Id);
        await AddCapacityAsync(alice.Id);

        var handler = ReadHandler(MemberUser());

        var result = await handler.Handle(
            new GetCapacityQuery(_repositoryId, _sprintId), CancellationToken.None);

        result.Members.Select(m => m.UserName).Should().ContainInOrder("Alice", "Zach");
    }

    [Fact]
    public async Task GetCapacity_ProjectsTheUserProfileOntoEachMember()
    {
        var alice = await AddUserAsync("Alice");
        await AddCapacityAsync(alice.Id);

        var handler = ReadHandler(MemberUser());

        var result = await handler.Handle(
            new GetCapacityQuery(_repositoryId, _sprintId), CancellationToken.None);

        var member = result.Members.Single();
        member.UserId.Should().Be(alice.Id);
        member.UserName.Should().Be("Alice");
        member.UserAvatar.Should().Be("bg-sky-600");
        member.Role.Should().Be("Developer");
        member.HoursPerDay.Should().Be(8m);
    }

    [Fact]
    public async Task GetCapacity_IgnoresMembersOfOtherSprints()
    {
        var alice = await AddUserAsync("Alice");
        var bob   = await AddUserAsync("Bob");
        await AddCapacityAsync(alice.Id);
        await AddCapacityAsync(bob.Id, sprintId: Guid.NewGuid());

        var handler = ReadHandler(MemberUser());

        var result = await handler.Handle(
            new GetCapacityQuery(_repositoryId, _sprintId), CancellationToken.None);

        result.Members.Should().ContainSingle().Which.UserName.Should().Be("Alice");
    }

    // ── GetCapacity: days off ───────────────────────────────────────────────

    [Fact]
    public async Task GetCapacity_ReturnsDaysOffOrderedByDate()
    {
        await AddDayOffAsync(date: new DateOnly(2026, 5, 14));
        await AddDayOffAsync(date: new DateOnly(2026, 5, 5));

        var handler = ReadHandler(MemberUser());

        var result = await handler.Handle(
            new GetCapacityQuery(_repositoryId, _sprintId), CancellationToken.None);

        result.DaysOff.Select(d => d.Date)
              .Should().ContainInOrder(new DateOnly(2026, 5, 5), new DateOnly(2026, 5, 14));
    }

    [Fact]
    public async Task GetCapacity_ResolvesTheUserNameOnPersonalDaysOff()
    {
        var alice = await AddUserAsync("Alice");
        await AddDayOffAsync(userId: alice.Id);

        var handler = ReadHandler(MemberUser());

        var result = await handler.Handle(
            new GetCapacityQuery(_repositoryId, _sprintId), CancellationToken.None);

        result.DaysOff.Single().UserName.Should().Be("Alice");
    }

    [Fact]
    public async Task GetCapacity_LeavesTheUserNameNullOnTeamWideDaysOff()
    {
        await AddDayOffAsync(userId: null);

        var handler = ReadHandler(MemberUser());

        var result = await handler.Handle(
            new GetCapacityQuery(_repositoryId, _sprintId), CancellationToken.None);

        var dayOff = result.DaysOff.Single();
        dayOff.UserId.Should().BeNull();
        dayOff.UserName.Should().BeNull("a team-wide entry names no individual");
    }

    [Fact]
    public async Task GetCapacity_IgnoresDaysOffOfOtherSprints()
    {
        await AddDayOffAsync();
        await AddDayOffAsync(sprintId: Guid.NewGuid());

        var handler = ReadHandler(MemberUser());

        var result = await handler.Handle(
            new GetCapacityQuery(_repositoryId, _sprintId), CancellationToken.None);

        result.DaysOff.Should().ContainSingle();
    }

    // ── RemoveDayOff ────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveDayOff_WithoutManageCapacityPrivilege_FailsAndKeepsTheEntry()
    {
        var dayOff  = await AddDayOffAsync();
        var handler = RemoveHandler(RequestUserContextMock.ForUser().Object);

        var result = await handler.Handle(
            new RemoveDayOffCommand(_repositoryId, _sprintId, dayOff.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("permission");
        (await _db.Set<DayOff>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task RemoveDayOff_WhenTheEntryDoesNotExist_ThrowsNotFound()
    {
        var handler = RemoveHandler(CapacityManager());

        var act = () => handler.Handle(
            new RemoveDayOffCommand(_repositoryId, _sprintId, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RemoveDayOff_WhenTheEntryBelongsToAnotherSprint_ThrowsNotFound()
    {
        var foreign = await AddDayOffAsync(sprintId: Guid.NewGuid());
        var handler = RemoveHandler(CapacityManager());

        var act = () => handler.Handle(
            new RemoveDayOffCommand(_repositoryId, _sprintId, foreign.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RemoveDayOff_DeletesOnlyTheNamedEntry()
    {
        var target    = await AddDayOffAsync(date: new DateOnly(2026, 5, 5));
        var bystander = await AddDayOffAsync(date: new DateOnly(2026, 5, 6));

        var handler = RemoveHandler(CapacityManager());

        var result = await handler.Handle(
            new RemoveDayOffCommand(_repositoryId, _sprintId, target.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _db.Set<DayOff>().AnyAsync(d => d.Id == target.Id)).Should().BeFalse();
        (await _db.Set<DayOff>().AnyAsync(d => d.Id == bystander.Id)).Should().BeTrue();
    }
}
