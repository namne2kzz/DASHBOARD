using System.Net;
using System.Net.Http.Json;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.IntegrationTests.Infrastructure;

namespace DASHBOARD.IntegrationTests.Queries;

/// <summary>
/// Runs the Overview dashboard aggregation against SQL Server.
/// </summary>
/// <remarks>
/// Overview is the heaviest read in the system and the one most exposed to translation failure: two
/// of its queries are <c>GroupBy</c> projections with conditional sums
/// (<c>g.Sum(t =&gt; t.State == Done ? t.StoryPoints : 0)</c>) filtered by <c>Contains</c> over a
/// local <c>HashSet</c>. The in-memory provider runs all of that in C# without complaint, so the
/// unit tests cannot tell whether SQL Server would accept it.
///
/// The assertions therefore target the parts that come back from those grouped queries —
/// velocity and the type trend — rather than the counts, which are computed in memory from an
/// already-materialised list and would pass even if the aggregation never translated.
///
/// Data is arranged across three sprints because both trends look back over several sprints; with a
/// single sprint the grouping collapses to one row and the ordering is never exercised.
/// </remarks>
[Collection(IntegrationTestCollection.Name)]
public sealed class OverviewQueryTests(SqlServerFixture database) : IAsyncLifetime
{
    private readonly ApiFactory _factory = new(database);

    private TenantSeed _tenant = null!;
    private HttpClient _client = null!;

    private Guid _oldestSprintId;
    private Guid _middleSprintId;
    private Guid _currentSprintId;

    /// <summary>Seeds three consecutive sprints with work items of differing types and states.</summary>
    public async Task InitializeAsync()
    {
        _tenant = await TestData.SeedTenantAsync(
            _factory, $"over{Guid.NewGuid():N}"[..14], "OVW");
        _client = await ApiClient.SignedInAsync(_factory, _tenant);

        await using var db = database.CreateContext();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var oldest = NewSprint("Sprint 1", today.AddDays(-40), today.AddDays(-27));
        var middle = NewSprint("Sprint 2", today.AddDays(-26), today.AddDays(-13));
        // The current sprint spans today, so SelectDefaultSprint picks it when no id is supplied.
        var current = NewSprint("Sprint 3", today.AddDays(-2), today.AddDays(11));

        db.Sprints.AddRange(oldest, middle, current);

        var number = 1;

        // Sprint 1: 5 points committed, all of it completed.
        db.SprintTasks.Add(NewTask(oldest.Id, SprintTaskType.UserStory, SprintTaskState.Done, 5, number++, closed: true));

        // Sprint 2: 8 points committed, 3 completed.
        db.SprintTasks.Add(NewTask(middle.Id, SprintTaskType.UserStory, SprintTaskState.Done, 3, number++, closed: true));
        db.SprintTasks.Add(NewTask(middle.Id, SprintTaskType.UserStory, SprintTaskState.Active, 5, number++));

        // Sprint 3: a mix of types so the type distribution and trend have something to separate.
        db.SprintTasks.Add(NewTask(current.Id, SprintTaskType.UserStory, SprintTaskState.Done,   2, number++, closed: true));
        db.SprintTasks.Add(NewTask(current.Id, SprintTaskType.Task,      SprintTaskState.Active, 3, number++));
        db.SprintTasks.Add(NewTask(current.Id, SprintTaskType.Bug,       SprintTaskState.Todo,   1, number++));
        db.SprintTasks.Add(NewTask(current.Id, SprintTaskType.Bug,       SprintTaskState.Todo,   1, number++));

        await db.SaveChangesAsync();

        _oldestSprintId  = oldest.Id;
        _middleSprintId  = middle.Id;
        _currentSprintId = current.Id;

        Sprint NewSprint(string name, DateOnly start, DateOnly end) => new()
        {
            RepositoryId = _tenant.RepositoryId,
            Name         = name,
            StartDate    = start,
            EndDate      = end,
        };

        SprintTask NewTask(
            Guid            sprintId,
            SprintTaskType  type,
            SprintTaskState state,
            int             points,
            int             workItemNumber,
            bool            closed = false) => new()
        {
            RepositoryId   = _tenant.RepositoryId,
            SprintId       = sprintId,
            Type           = type,
            State          = state,
            Title          = $"{type} {workItemNumber}",
            WorkItemNumber = workItemNumber,
            StoryPoints    = points,
            ClosedAt       = closed ? DateTime.UtcNow.AddDays(-1) : null,
        };
    }

    /// <summary>Disposes the client and the API host.</summary>
    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private async Task<OverviewStats> GetAsync(Guid? sprintId = null)
    {
        var route = $"{ApiClient.V1}/repositories/{_tenant.RepositoryId}/overview"
                  + (sprintId.HasValue ? $"?sprintId={sprintId}" : string.Empty);

        var response = await _client.GetAsync(route);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<OverviewStats>())!;
    }

    // ── The query translates at all ─────────────────────────────────────────

    [Fact]
    public async Task TheOverviewAggregationTranslates()
    {
        // The blunt case: if either GroupBy projection fails to translate, the request throws and
        // the status assertion inside GetAsync fails.
        var stats = await GetAsync();

        stats.Should().NotBeNull();
        stats.AvailableSprints.Should().HaveCount(3);
    }

    [Fact]
    public async Task WithNoSprintSpecified_ItSelectsTheSprintContainingToday()
    {
        var stats = await GetAsync();

        stats.WorkItemCounts.Total.Should().Be(4, "Sprint 3 is the one spanning today");
    }

    // ── Velocity: GroupBy with a conditional Sum ────────────────────────────

    [Fact]
    public async Task VelocitySeparatesCommittedFromCompletedPoints()
    {
        // This is the conditional-sum projection. A translation that ignored the condition would
        // report committed and completed as the same number.
        var stats = await GetAsync();

        var sprint2 = stats.VelocityTrend.Single(v => v.SprintName == "Sprint 2");
        sprint2.CommittedPoints.Should().Be(8);
        sprint2.CompletedPoints.Should().Be(3, "only the Done item counts toward completed points");
    }

    [Fact]
    public async Task VelocityCoversEverySprintAndRunsOldestFirst()
    {
        var stats = await GetAsync();

        // Note the array: Equal's params overload would swallow a "because" string as a fourth
        // expected element.
        stats.VelocityTrend.Select(v => v.SprintName)
            .Should().Equal(["Sprint 1", "Sprint 2", "Sprint 3"],
                "the trend is ordered by start date so a chart reads left to right");
    }

    [Fact]
    public async Task ASprintWhereEverythingWasFinishedReportsEqualCommittedAndCompleted()
    {
        var stats = await GetAsync();

        var sprint1 = stats.VelocityTrend.Single(v => v.SprintName == "Sprint 1");
        sprint1.CommittedPoints.Should().Be(5);
        sprint1.CompletedPoints.Should().Be(5);
    }

    // ── Type trend: GroupBy with several conditional Counts ─────────────────

    [Fact]
    public async Task TheTypeTrendCountsEachWorkItemTypeSeparately()
    {
        var stats = await GetAsync(_currentSprintId);

        var current = stats.TypeTrend.Single(t => t.SprintName == "Sprint 3");
        current.UserStory.Should().Be(1);
        current.Task.Should().Be(1);
        current.Bug.Should().Be(2);
        current.TestPlan.Should().Be(0);
    }

    [Fact]
    public async Task TheTypeTrendLooksBackFromTheSelectedSprint()
    {
        // Selecting the middle sprint must show it and the ones before it, not the ones after.
        var stats = await GetAsync(_middleSprintId);

        stats.TypeTrend.Select(t => t.SprintName)
            .Should().Equal("Sprint 1", "Sprint 2");
    }

    // ── Distributions over the selected sprint ──────────────────────────────

    [Fact]
    public async Task TheStatusDistributionMatchesTheSelectedSprint()
    {
        var stats = await GetAsync(_currentSprintId);

        stats.StatusDistribution.Done.Should().Be(1);
        stats.StatusDistribution.Active.Should().Be(1);
        stats.StatusDistribution.Todo.Should().Be(2);
    }

    [Fact]
    public async Task StoryPointsSeparateTheCommittedTotalFromTheCompletedOne()
    {
        var stats = await GetAsync(_currentSprintId);

        stats.StoryPoints.Committed.Should().Be(7);
        stats.StoryPoints.Completed.Should().Be(2);
    }

    [Fact]
    public async Task SelectingAnEarlierSprintChangesTheCounts()
    {
        var stats = await GetAsync(_oldestSprintId);

        stats.WorkItemCounts.Total.Should().Be(1);
        stats.WorkItemCounts.UserStories.Should().Be(1);
        stats.WorkItemCounts.Bugs.Should().Be(0);
    }

    // ── Edge cases ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AnUnknownSprintIdIs404()
    {
        var response = await _client.GetAsync(
            $"{ApiClient.V1}/repositories/{_tenant.RepositoryId}/overview?sprintId={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ARepositoryWithNoSprintsStillAnswers()
    {
        // The handler short-circuits to an empty task list when no sprint is selected. That path
        // still runs the velocity query against an empty id set, which is its own translation risk.
        var empty = await TestData.SeedTenantAsync(
            _factory, $"bare{Guid.NewGuid():N}"[..14], "BAR");

        using var client = await ApiClient.SignedInAsync(_factory, empty);

        var response = await client.GetAsync(
            $"{ApiClient.V1}/repositories/{empty.RepositoryId}/overview");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var stats = (await response.Content.ReadFromJsonAsync<OverviewStats>())!;
        stats.AvailableSprints.Should().BeEmpty();
        stats.WorkItemCounts.Total.Should().Be(0);
        stats.VelocityTrend.Should().BeEmpty();
    }

    [Fact]
    public async Task SoftDeletedTasksAreLeftOutOfTheAggregation()
    {
        await using (var db = database.CreateContext())
        {
            var bug = db.SprintTasks.First(t => t.SprintId == _currentSprintId && t.Type == SprintTaskType.Bug);
            db.SprintTasks.Remove(bug);
            await db.SaveChangesAsync();
        }

        var stats = await GetAsync(_currentSprintId);

        stats.WorkItemCounts.Bugs.Should().Be(1, "the removed bug is soft-deleted and must not be counted");
        stats.TypeTrend.Single(t => t.SprintName == "Sprint 3").Bug.Should().Be(1,
            "the grouped query filters on IsDeleted too, not only the in-memory count");
    }

    // ── Response shapes, local to this file ─────────────────────────────────

    private sealed record OverviewStats(
        WorkItemCounts           WorkItemCounts,
        StatusDistribution       StatusDistribution,
        List<SprintRef>          AvailableSprints,
        StoryPoints              StoryPoints,
        List<VelocityTrendItem>  VelocityTrend,
        List<SprintTypeTrendItem> TypeTrend);

    private sealed record WorkItemCounts(int Total, int Bugs, int UserStories, int Tasks, int TestPlans);

    private sealed record StatusDistribution(int Todo, int Active, int InReview, int Done, int New, int Backlog);

    private sealed record SprintRef(Guid Id, string Name);

    private sealed record StoryPoints(int Committed, int Completed);

    private sealed record VelocityTrendItem(string SprintName, int CommittedPoints, int CompletedPoints);

    private sealed record SprintTypeTrendItem(string SprintName, int UserStory, int Task, int Bug, int TestPlan);
}
