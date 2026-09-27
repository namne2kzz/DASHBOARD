using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Sprints.Queries.GetSprintDetail;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.UnitTests.Common;

namespace DASHBOARD.UnitTests.Application.Sprints.Queries;

/// <summary>Unit tests for <see cref="GetSprintDetailQueryHandler"/>, focused on the computed member loads.</summary>
public sealed class GetSprintDetailQueryHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database with the owning repository already present.</summary>
    public GetSprintDetailQueryHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;

        // The handler reads Repository.Code with First(), so the repository row must exist.
        _db.Set<Repository>().Add(
            new Repository { Name = "Dashboard", Code = "DASH" }.WithId(_repositoryId));
        _db.SaveChanges();
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private GetSprintDetailQueryHandler CreateHandler(IRequestUserContext user) => new(_db, user);

    private IRequestUserContext MemberUser() =>
        RequestUserContextMock.ForUser().AsMember(_repositoryId).Object;

    /// <summary>Seeds a sprint spanning Mon 2026-05-04 → Fri 2026-05-15: 10 working days.</summary>
    private async Task<Sprint> SeedSprintAsync(Guid? repositoryId = null)
    {
        var sprint = new Sprint
        {
            RepositoryId = repositoryId ?? _repositoryId,
            Name         = "Sprint 1",
            StartDate    = new DateOnly(2026, 5, 4),
            EndDate      = new DateOnly(2026, 5, 15),
        };
        _db.Set<Sprint>().Add(sprint);
        await _db.SaveChangesAsync();
        return sprint;
    }

    private async Task<User> AddUserAsync(string name)
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

    private async Task AddCapacityAsync(Guid sprintId, Guid userId, decimal hoursPerDay, decimal overtime = 0m)
    {
        _db.Set<CapacityMember>().Add(new CapacityMember
        {
            SprintId            = sprintId,
            RepositoryId        = _repositoryId,
            UserId              = userId,
            HoursPerDay         = hoursPerDay,
            OvertimeHoursPerDay = overtime,
        });
        await _db.SaveChangesAsync();
    }

    private async Task AddDayOffAsync(Guid sprintId, Guid? userId, decimal hours)
    {
        _db.Set<DayOff>().Add(new DayOff
        {
            SprintId     = sprintId,
            RepositoryId = _repositoryId,
            UserId       = userId,
            Date         = new DateOnly(2026, 5, 6),
            Hours        = hours,
        });
        await _db.SaveChangesAsync();
    }

    private async Task AddTaskAsync(
        Guid sprintId, Guid? assignedToId, decimal remainingWork, SprintTaskState state)
    {
        _db.Set<SprintTask>().Add(new SprintTask
        {
            SprintId      = sprintId,
            RepositoryId  = _repositoryId,
            Title         = "Task",
            AssignedToId  = assignedToId,
            RemainingWork = remainingWork,
            State         = state,
        });
        await _db.SaveChangesAsync();
    }

    // ── Access control ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserIsNotAMember_Throws()
    {
        var sprint  = await SeedSprintAsync();
        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(
            new GetSprintDetailQuery(_repositoryId, sprint.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_WhenSprintDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(MemberUser());

        var act = () => handler.Handle(
            new GetSprintDetailQuery(_repositoryId, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Sprint metadata ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsSprintMetadataAndWorkingDays()
    {
        var sprint  = await SeedSprintAsync();
        var handler = CreateHandler(MemberUser());

        var detail = await handler.Handle(
            new GetSprintDetailQuery(_repositoryId, sprint.Id), CancellationToken.None);

        detail.Id.Should().Be(sprint.Id);
        detail.Name.Should().Be("Sprint 1");
        detail.WorkingDays.Should().Be(10);
        detail.IsActive.Should().BeFalse("the seeded range is in the past");
    }

    // ── Member load: capacity ───────────────────────────────────────────────

    [Fact]
    public async Task Handle_ComputesGrossCapacityFromHoursAndOvertime()
    {
        var sprint = await SeedSprintAsync();
        var alice  = await AddUserAsync("Alice");
        await AddCapacityAsync(sprint.Id, alice.Id, hoursPerDay: 6m, overtime: 2m);

        var handler = CreateHandler(MemberUser());

        var detail = await handler.Handle(
            new GetSprintDetailQuery(_repositoryId, sprint.Id), CancellationToken.None);

        detail.MemberLoads.Should().ContainSingle();
        detail.MemberLoads[0].Capacity.Should().Be((6m + 2m) * 10);
    }

    [Fact]
    public async Task Handle_SubtractsPersonalDaysOffFromThatMemberOnly()
    {
        var sprint = await SeedSprintAsync();
        var alice  = await AddUserAsync("Alice");
        var bob    = await AddUserAsync("Bob");
        await AddCapacityAsync(sprint.Id, alice.Id, hoursPerDay: 8m);
        await AddCapacityAsync(sprint.Id, bob.Id,   hoursPerDay: 8m);
        await AddDayOffAsync(sprint.Id, alice.Id, hours: 8m);

        var handler = CreateHandler(MemberUser());

        var detail = await handler.Handle(
            new GetSprintDetailQuery(_repositoryId, sprint.Id), CancellationToken.None);

        var aliceLoad = detail.MemberLoads.Single(m => m.UserId == alice.Id);
        var bobLoad   = detail.MemberLoads.Single(m => m.UserId == bob.Id);

        aliceLoad.PersonalDaysOffHours.Should().Be(8m);
        aliceLoad.Capacity.Should().Be(8m * 10 - 8m);
        bobLoad.PersonalDaysOffHours.Should().Be(0m);
        bobLoad.Capacity.Should().Be(8m * 10);
    }

    [Fact]
    public async Task Handle_CapsTeamWideDayOffAtTheMembersDailyHours()
    {
        var sprint   = await SeedSprintAsync();
        var partTime = await AddUserAsync("PartTime");
        await AddCapacityAsync(sprint.Id, partTime.Id, hoursPerDay: 4m);

        // A 8 h team holiday cannot remove more than this member's 4 h working day.
        await AddDayOffAsync(sprint.Id, userId: null, hours: 8m);

        var handler = CreateHandler(MemberUser());

        var detail = await handler.Handle(
            new GetSprintDetailQuery(_repositoryId, sprint.Id), CancellationToken.None);

        detail.MemberLoads[0].Capacity.Should().Be(4m * 10 - 4m);
    }

    [Fact]
    public async Task Handle_WhenDaysOffExceedCapacity_ClampsCapacityToZero()
    {
        var sprint = await SeedSprintAsync();
        var alice  = await AddUserAsync("Alice");
        await AddCapacityAsync(sprint.Id, alice.Id, hoursPerDay: 8m);
        await AddDayOffAsync(sprint.Id, alice.Id, hours: 500m);

        var handler = CreateHandler(MemberUser());

        var detail = await handler.Handle(
            new GetSprintDetailQuery(_repositoryId, sprint.Id), CancellationToken.None);

        detail.MemberLoads[0].Capacity.Should().Be(0m);
    }

    // ── Member load: workload ───────────────────────────────────────────────

    [Fact]
    public async Task Handle_SumsRemainingWorkOfOpenTasksAssignedToTheMember()
    {
        var sprint = await SeedSprintAsync();
        var alice  = await AddUserAsync("Alice");
        await AddCapacityAsync(sprint.Id, alice.Id, hoursPerDay: 8m);
        await AddTaskAsync(sprint.Id, alice.Id, remainingWork: 10m, state: SprintTaskState.Active);
        await AddTaskAsync(sprint.Id, alice.Id, remainingWork: 5m,  state: SprintTaskState.Todo);

        var handler = CreateHandler(MemberUser());

        var detail = await handler.Handle(
            new GetSprintDetailQuery(_repositoryId, sprint.Id), CancellationToken.None);

        detail.MemberLoads[0].Workload.Should().Be(15m);
    }

    [Fact]
    public async Task Handle_ExcludesDoneTasksFromWorkload()
    {
        var sprint = await SeedSprintAsync();
        var alice  = await AddUserAsync("Alice");
        await AddCapacityAsync(sprint.Id, alice.Id, hoursPerDay: 8m);
        await AddTaskAsync(sprint.Id, alice.Id, remainingWork: 10m, state: SprintTaskState.Active);
        await AddTaskAsync(sprint.Id, alice.Id, remainingWork: 99m, state: SprintTaskState.Done);

        var handler = CreateHandler(MemberUser());

        var detail = await handler.Handle(
            new GetSprintDetailQuery(_repositoryId, sprint.Id), CancellationToken.None);

        detail.MemberLoads[0].Workload.Should().Be(10m);
    }

    [Fact]
    public async Task Handle_ExcludesTasksAssignedToOtherMembers()
    {
        var sprint = await SeedSprintAsync();
        var alice  = await AddUserAsync("Alice");
        var bob    = await AddUserAsync("Bob");
        await AddCapacityAsync(sprint.Id, alice.Id, hoursPerDay: 8m);
        await AddTaskAsync(sprint.Id, alice.Id, remainingWork: 10m, state: SprintTaskState.Active);
        await AddTaskAsync(sprint.Id, bob.Id,   remainingWork: 99m, state: SprintTaskState.Active);
        await AddTaskAsync(sprint.Id, null,     remainingWork: 77m, state: SprintTaskState.Active);

        var handler = CreateHandler(MemberUser());

        var detail = await handler.Handle(
            new GetSprintDetailQuery(_repositoryId, sprint.Id), CancellationToken.None);

        detail.MemberLoads.Single(m => m.UserId == alice.Id).Workload.Should().Be(10m);
    }

    // ── Member load: percentage and state ───────────────────────────────────

    [Theory]
    // capacity is 80 h over 10 days at 8 h/day
    [InlineData(0,  0,   "safe")]
    [InlineData(40, 50,  "safe")]
    [InlineData(80, 100, "safe")]      // exactly at capacity is still safe
    [InlineData(88, 110, "warning")]   // over 100 %
    [InlineData(96, 120, "warning")]   // exactly 120 % is still a warning
    [InlineData(100, 125, "overloaded")]
    public async Task Handle_DerivesLoadPercentAndState(
        int remainingWork, int expectedPercent, string expectedState)
    {
        var sprint = await SeedSprintAsync();
        var alice  = await AddUserAsync("Alice");
        await AddCapacityAsync(sprint.Id, alice.Id, hoursPerDay: 8m);
        if (remainingWork > 0)
            await AddTaskAsync(sprint.Id, alice.Id, remainingWork, SprintTaskState.Active);

        var handler = CreateHandler(MemberUser());

        var detail = await handler.Handle(
            new GetSprintDetailQuery(_repositoryId, sprint.Id), CancellationToken.None);

        detail.MemberLoads[0].LoadPercent.Should().Be(expectedPercent);
        detail.MemberLoads[0].LoadState.Should().Be(expectedState);
    }

    [Fact]
    public async Task Handle_WhenCapacityIsZeroAndWorkAssigned_ReportsSentinelOverload()
    {
        var sprint = await SeedSprintAsync();
        var alice  = await AddUserAsync("Alice");
        await AddCapacityAsync(sprint.Id, alice.Id, hoursPerDay: 0m);
        await AddTaskAsync(sprint.Id, alice.Id, remainingWork: 5m, state: SprintTaskState.Active);

        var handler = CreateHandler(MemberUser());

        var detail = await handler.Handle(
            new GetSprintDetailQuery(_repositoryId, sprint.Id), CancellationToken.None);

        detail.MemberLoads[0].LoadPercent.Should().Be(999, "a divide-by-zero capacity uses a sentinel");
        detail.MemberLoads[0].LoadState.Should().Be("overloaded");
    }

    [Fact]
    public async Task Handle_WhenCapacityIsZeroAndNoWorkAssigned_ReportsZeroLoad()
    {
        var sprint = await SeedSprintAsync();
        var alice  = await AddUserAsync("Alice");
        await AddCapacityAsync(sprint.Id, alice.Id, hoursPerDay: 0m);

        var handler = CreateHandler(MemberUser());

        var detail = await handler.Handle(
            new GetSprintDetailQuery(_repositoryId, sprint.Id), CancellationToken.None);

        detail.MemberLoads[0].LoadPercent.Should().Be(0);
        detail.MemberLoads[0].LoadState.Should().Be("safe");
    }

    // ── Scoping ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_IgnoresCapacityRowsFromOtherSprints()
    {
        var sprint = await SeedSprintAsync();
        var other  = await SeedSprintAsync();
        var alice  = await AddUserAsync("Alice");
        var bob    = await AddUserAsync("Bob");
        await AddCapacityAsync(sprint.Id, alice.Id, hoursPerDay: 8m);
        await AddCapacityAsync(other.Id,  bob.Id,   hoursPerDay: 8m);

        var handler = CreateHandler(MemberUser());

        var detail = await handler.Handle(
            new GetSprintDetailQuery(_repositoryId, sprint.Id), CancellationToken.None);

        detail.MemberLoads.Should().ContainSingle();
        detail.MemberLoads[0].UserId.Should().Be(alice.Id);
    }
}
