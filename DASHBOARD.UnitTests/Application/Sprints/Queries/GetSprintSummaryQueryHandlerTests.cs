using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Sprints.Queries.GetSprintSummary;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.UnitTests.Common;

namespace DASHBOARD.UnitTests.Application.Sprints.Queries;

/// <summary>Unit tests for <see cref="GetSprintSummaryQueryHandler"/>.</summary>
public sealed class GetSprintSummaryQueryHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public GetSprintSummaryQueryHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private GetSprintSummaryQueryHandler CreateHandler(IRequestUserContext user) => new(_db, user);

    private IRequestUserContext MemberUser() =>
        RequestUserContextMock.ForUser().AsMember(_repositoryId).Object;

    /// <summary>Seeds a sprint. Defaults to Mon 2026-05-04 → Fri 2026-05-15: two full working weeks, 10 working days.</summary>
    private async Task<Sprint> SeedSprintAsync(DateOnly? start = null, DateOnly? end = null, Guid? repositoryId = null)
    {
        var sprint = new Sprint
        {
            RepositoryId = repositoryId ?? _repositoryId,
            Name         = "Sprint 1",
            StartDate    = start ?? new DateOnly(2026, 5, 4),
            EndDate      = end   ?? new DateOnly(2026, 5, 15),
        };
        _db.Set<Sprint>().Add(sprint);
        await _db.SaveChangesAsync();
        return sprint;
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

    private async Task AddTaskAsync(Guid sprintId, int storyPoints, WorkItemState state)
    {
        _db.Set<SprintTask>().Add(new SprintTask
        {
            SprintId     = sprintId,
            RepositoryId = _repositoryId,
            Title        = "Task",
            StoryPoints  = storyPoints,
            State        = state,
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
            new GetSprintSummaryQuery(_repositoryId, sprint.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_WhenSprintDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(MemberUser());

        var act = () => handler.Handle(
            new GetSprintSummaryQuery(_repositoryId, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenSprintBelongsToAnotherRepository_ThrowsNotFound()
    {
        var foreign = await SeedSprintAsync(repositoryId: Guid.NewGuid());
        var handler = CreateHandler(MemberUser());

        var act = () => handler.Handle(
            new GetSprintSummaryQuery(_repositoryId, foreign.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Working days ────────────────────────────────────────────────────────

    [Theory]
    // Mon 4 May → Fri 15 May 2026: two full working weeks.
    [InlineData("2026-05-04", "2026-05-15", 10)]
    // Mon → Fri, one week.
    [InlineData("2026-05-04", "2026-05-08", 5)]
    // A single weekday.
    [InlineData("2026-05-04", "2026-05-04", 1)]
    // Sat → Sun only: no working days at all.
    [InlineData("2026-05-09", "2026-05-10", 0)]
    // Starts on a Saturday, ends on the following Friday.
    [InlineData("2026-05-09", "2026-05-15", 5)]
    public async Task Handle_CountsWorkingDaysExcludingWeekends(string start, string end, int expected)
    {
        var sprint  = await SeedSprintAsync(DateOnly.Parse(start), DateOnly.Parse(end));
        var handler = CreateHandler(MemberUser());

        var summary = await handler.Handle(
            new GetSprintSummaryQuery(_repositoryId, sprint.Id), CancellationToken.None);

        summary.TotalWorkingDays.Should().Be(expected);
    }

    // ── Capacity ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithNoCapacityRows_ReportsZeroCapacity()
    {
        var sprint  = await SeedSprintAsync();
        var handler = CreateHandler(MemberUser());

        var summary = await handler.Handle(
            new GetSprintSummaryQuery(_repositoryId, sprint.Id), CancellationToken.None);

        summary.TotalCapacityHours.Should().Be(0m);
    }

    [Fact]
    public async Task Handle_SumsHoursPerDayAndOvertimeAcrossWorkingDays()
    {
        var sprint = await SeedSprintAsync();
        await AddCapacityAsync(sprint.Id, Guid.NewGuid(), hoursPerDay: 6m, overtime: 2m);

        var handler = CreateHandler(MemberUser());

        var summary = await handler.Handle(
            new GetSprintSummaryQuery(_repositoryId, sprint.Id), CancellationToken.None);

        summary.TotalCapacityHours.Should().Be((6m + 2m) * 10);
    }

    [Fact]
    public async Task Handle_SumsCapacityAcrossMultipleMembers()
    {
        var sprint = await SeedSprintAsync();
        await AddCapacityAsync(sprint.Id, Guid.NewGuid(), hoursPerDay: 8m);
        await AddCapacityAsync(sprint.Id, Guid.NewGuid(), hoursPerDay: 4m);

        var handler = CreateHandler(MemberUser());

        var summary = await handler.Handle(
            new GetSprintSummaryQuery(_repositoryId, sprint.Id), CancellationToken.None);

        summary.TotalCapacityHours.Should().Be(8m * 10 + 4m * 10);
    }

    [Fact]
    public async Task Handle_SubtractsPersonalDaysOffFromThatMemberOnly()
    {
        var sprint  = await SeedSprintAsync();
        var alice   = Guid.NewGuid();
        var bob     = Guid.NewGuid();
        await AddCapacityAsync(sprint.Id, alice, hoursPerDay: 8m);
        await AddCapacityAsync(sprint.Id, bob,   hoursPerDay: 8m);
        await AddDayOffAsync(sprint.Id, alice, hours: 8m);

        var handler = CreateHandler(MemberUser());

        var summary = await handler.Handle(
            new GetSprintSummaryQuery(_repositoryId, sprint.Id), CancellationToken.None);

        summary.TotalCapacityHours.Should().Be(8m * 10 - 8m + 8m * 10);
    }

    [Fact]
    public async Task Handle_SubtractsTeamWideDayOffFromEveryMember()
    {
        var sprint = await SeedSprintAsync();
        await AddCapacityAsync(sprint.Id, Guid.NewGuid(), hoursPerDay: 8m);
        await AddCapacityAsync(sprint.Id, Guid.NewGuid(), hoursPerDay: 8m);

        // A null UserId marks a day off that applies to the whole team.
        await AddDayOffAsync(sprint.Id, userId: null, hours: 8m);

        var handler = CreateHandler(MemberUser());

        var summary = await handler.Handle(
            new GetSprintSummaryQuery(_repositoryId, sprint.Id), CancellationToken.None);

        summary.TotalCapacityHours.Should().Be((8m * 10 - 8m) * 2);
    }

    [Fact]
    public async Task Handle_CapsTeamWideDayOffAtTheMembersDailyHours()
    {
        var sprint = await SeedSprintAsync();
        await AddCapacityAsync(sprint.Id, Guid.NewGuid(), hoursPerDay: 4m);

        // An 8 h company holiday costs a 4 h/day member only the 4 h they would have worked.
        await AddDayOffAsync(sprint.Id, userId: null, hours: 8m);

        var handler = CreateHandler(MemberUser());

        var summary = await handler.Handle(
            new GetSprintSummaryQuery(_repositoryId, sprint.Id), CancellationToken.None);

        summary.TotalCapacityHours.Should().Be(4m * 10 - 4m,
            "this must match how GetSprintDetail computes the same figure");
    }

    [Fact]
    public async Task Handle_CapsTeamWideDayOffPerMemberNotGlobally()
    {
        var sprint = await SeedSprintAsync();
        await AddCapacityAsync(sprint.Id, Guid.NewGuid(), hoursPerDay: 8m);
        await AddCapacityAsync(sprint.Id, Guid.NewGuid(), hoursPerDay: 4m);
        await AddDayOffAsync(sprint.Id, userId: null, hours: 8m);

        var handler = CreateHandler(MemberUser());

        var summary = await handler.Handle(
            new GetSprintSummaryQuery(_repositoryId, sprint.Id), CancellationToken.None);

        summary.TotalCapacityHours.Should().Be((8m * 10 - 8m) + (4m * 10 - 4m),
            "the full-timer loses 8 h and the part-timer only 4 h");
    }

    [Fact]
    public async Task Handle_WhenDaysOffExceedCapacity_ClampsToZero()
    {
        // One working day (a Monday) gives 8 h, while the day off removes 40 h.
        var sprint = await SeedSprintAsync(new DateOnly(2026, 5, 4), new DateOnly(2026, 5, 4));
        var user   = Guid.NewGuid();
        await AddCapacityAsync(sprint.Id, user, hoursPerDay: 8m);
        await AddDayOffAsync(sprint.Id, user, hours: 40m);

        var handler = CreateHandler(MemberUser());

        var summary = await handler.Handle(
            new GetSprintSummaryQuery(_repositoryId, sprint.Id), CancellationToken.None);

        summary.TotalCapacityHours.Should().Be(0m, "capacity is never reported as negative");
    }

    [Fact]
    public async Task Handle_ClampsPerMemberSoOneAbsenceCannotEatAnothersCapacity()
    {
        var sprint  = await SeedSprintAsync(new DateOnly(2026, 5, 4), new DateOnly(2026, 5, 4));
        var absent  = Guid.NewGuid();
        var present = Guid.NewGuid();
        await AddCapacityAsync(sprint.Id, absent,  hoursPerDay: 8m);
        await AddCapacityAsync(sprint.Id, present, hoursPerDay: 8m);
        await AddDayOffAsync(sprint.Id, absent, hours: 40m);

        var handler = CreateHandler(MemberUser());

        var summary = await handler.Handle(
            new GetSprintSummaryQuery(_repositoryId, sprint.Id), CancellationToken.None);

        summary.TotalCapacityHours.Should().Be(8m,
            "the absent member floors at 0 h, leaving the present member's 8 h intact");
    }

    [Fact]
    public async Task Handle_IgnoresCapacityAndDaysOffFromOtherSprints()
    {
        var sprint      = await SeedSprintAsync();
        var otherSprint = await SeedSprintAsync(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 12));
        await AddCapacityAsync(sprint.Id,      Guid.NewGuid(), hoursPerDay: 8m);
        await AddCapacityAsync(otherSprint.Id, Guid.NewGuid(), hoursPerDay: 8m);

        var handler = CreateHandler(MemberUser());

        var summary = await handler.Handle(
            new GetSprintSummaryQuery(_repositoryId, sprint.Id), CancellationToken.None);

        summary.TotalCapacityHours.Should().Be(8m * 10);
    }

    // ── Task and story-point aggregation ────────────────────────────────────

    [Fact]
    public async Task Handle_WithNoTasks_ReportsZeroes()
    {
        var sprint  = await SeedSprintAsync();
        var handler = CreateHandler(MemberUser());

        var summary = await handler.Handle(
            new GetSprintSummaryQuery(_repositoryId, sprint.Id), CancellationToken.None);

        summary.TotalTasks.Should().Be(0);
        summary.CompletedTasks.Should().Be(0);
        summary.CommittedStoryPoints.Should().Be(0);
        summary.CompletedStoryPoints.Should().Be(0);
    }

    [Fact]
    public async Task Handle_CountsOnlyDoneTasksAsCompleted()
    {
        var sprint = await SeedSprintAsync();
        await AddTaskAsync(sprint.Id, storyPoints: 3, state: WorkItemState.Done);
        await AddTaskAsync(sprint.Id, storyPoints: 5, state: WorkItemState.InProgress);
        await AddTaskAsync(sprint.Id, storyPoints: 2, state: WorkItemState.Open);

        var handler = CreateHandler(MemberUser());

        var summary = await handler.Handle(
            new GetSprintSummaryQuery(_repositoryId, sprint.Id), CancellationToken.None);

        summary.TotalTasks.Should().Be(3);
        summary.CompletedTasks.Should().Be(1);
        summary.CommittedStoryPoints.Should().Be(10);
        summary.CompletedStoryPoints.Should().Be(3);
    }

    [Fact]
    public async Task Handle_IgnoresTasksFromOtherSprints()
    {
        var sprint      = await SeedSprintAsync();
        var otherSprint = await SeedSprintAsync(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 12));
        await AddTaskAsync(sprint.Id,      storyPoints: 3, state: WorkItemState.Done);
        await AddTaskAsync(otherSprint.Id, storyPoints: 8, state: WorkItemState.Done);

        var handler = CreateHandler(MemberUser());

        var summary = await handler.Handle(
            new GetSprintSummaryQuery(_repositoryId, sprint.Id), CancellationToken.None);

        summary.TotalTasks.Should().Be(1);
        summary.CommittedStoryPoints.Should().Be(3);
    }

    [Fact]
    public async Task Handle_ReturnsSprintIdentity()
    {
        var sprint  = await SeedSprintAsync();
        var handler = CreateHandler(MemberUser());

        var summary = await handler.Handle(
            new GetSprintSummaryQuery(_repositoryId, sprint.Id), CancellationToken.None);

        summary.SprintId.Should().Be(sprint.Id);
        summary.SprintName.Should().Be("Sprint 1");
    }
}
