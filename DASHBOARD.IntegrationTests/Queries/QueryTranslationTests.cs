using System.Net;
using System.Net.Http.Json;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.IntegrationTests.Queries;

/// <summary>
/// Runs the application's more elaborate read queries against SQL Server.
/// </summary>
/// <remarks>
/// These queries pass their unit tests because the in-memory provider evaluates LINQ in process:
/// anything expressible in C# "works". SQL Server is stricter — a left join, a group-by projection
/// or a <c>Contains</c> over a local list either translates or throws at runtime. That difference is
/// only observable here, which makes these tests about translation rather than about results.
/// </remarks>
[Collection(IntegrationTestCollection.Name)]
public sealed class QueryTranslationTests(SqlServerFixture database) : IAsyncLifetime
{
    private readonly ApiFactory _factory = new(database);

    private TenantSeed _tenant = null!;
    private HttpClient _client = null!;
    private Guid       _sprintId;

    /// <summary>Seeds a tenant with a sprint, some work items and a backlog item.</summary>
    public async Task InitializeAsync()
    {
        _tenant = await TestData.SeedTenantAsync(
            _factory, $"query{Guid.NewGuid():N}"[..14], "QRY");
        _client = await ApiClient.SignedInAsync(_factory, _tenant);

        await using var db = database.CreateContext();

        var sprint = new Sprint
        {
            RepositoryId = _tenant.RepositoryId,
            Name         = "Query sprint",
            StartDate    = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            EndDate      = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(12)),
        };
        db.Sprints.Add(sprint);

        // A story with a sub-task, so tree building and the left join to Sprint both have data.
        var story = new SprintTask
        {
            RepositoryId   = _tenant.RepositoryId,
            SprintId       = sprint.Id,
            Type           = SprintTaskType.UserStory,
            Title          = "Checkout flow",
            WorkItemNumber = 1,
            State          = SprintTaskState.Active,
            StoryPoints    = 8,
        };
        db.SprintTasks.Add(story);

        db.SprintTasks.Add(new SprintTask
        {
            RepositoryId   = _tenant.RepositoryId,
            SprintId       = sprint.Id,
            ParentId       = story.Id,
            Type           = SprintTaskType.Task,
            Title          = "Wire up the login form",
            WorkItemNumber = 2,
            State          = SprintTaskState.Todo,
            AssignedToId   = _tenant.UserId,
            RemainingWork  = 4m,
        });

        // A standalone item with no sprint, to prove the left join keeps unscheduled rows.
        db.SprintTasks.Add(new SprintTask
        {
            RepositoryId   = _tenant.RepositoryId,
            SprintId       = null,
            Type           = SprintTaskType.Bug,
            Title          = "Unscheduled login bug",
            WorkItemNumber = 3,
            State          = SprintTaskState.Backlog,
            AssignedToId   = _tenant.UserId,
        });

        db.BacklogItems.Add(new BacklogItem
        {
            RepositoryId       = _tenant.RepositoryId,
            Type               = BacklogItemType.UserStory,
            State              = BacklogItemState.Ready,
            Title              = "Login rework",
            Rank               = 1000m,
            AcceptanceCriteria = "Given a login attempt…",
        });

        await db.SaveChangesAsync();
        _sprintId = sprint.Id;
    }

    /// <summary>Disposes the client and the API host.</summary>
    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // ── MyWork: left join plus an in-memory work-item key ───────────────────

    [Fact]
    public async Task MyWork_TranslatesAndReturnsAssignedWork()
    {
        var response = await _client.GetAsync($"{ApiClient.V1}/my-work");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = await response.Content.ReadFromJsonAsync<List<MyWorkRow>>();
        items.Should().NotBeNull();
        items!.Should().HaveCount(2, "one sprint task and one standalone item are assigned");
    }

    [Fact]
    public async Task MyWork_KeepsItemsThatBelongToNoSprint()
    {
        var response = await _client.GetAsync($"{ApiClient.V1}/my-work");
        var items    = await response.Content.ReadFromJsonAsync<List<MyWorkRow>>();

        var unscheduled = items!.Single(i => i.Title == "Unscheduled login bug");
        unscheduled.SprintId.Should().BeNull();
        unscheduled.SprintName.Should().BeNull(
            "the left join must not drop rows whose SprintId is null");
    }

    [Fact]
    public async Task MyWork_ResolvesTheSprintNameForScheduledWork()
    {
        var response = await _client.GetAsync($"{ApiClient.V1}/my-work");
        var items    = await response.Content.ReadFromJsonAsync<List<MyWorkRow>>();

        var scheduled = items!.Single(i => i.Title == "Wire up the login form");
        scheduled.SprintId.Should().Be(_sprintId);
        scheduled.SprintName.Should().Be("Query sprint");
    }

    // ── Search: string matching plus a numeric key parse ────────────────────

    [Fact]
    public async Task Search_MatchesTitlesAcrossTasksAndBacklog()
    {
        var response = await _client.GetAsync(
            $"{ApiClient.V1}/repositories/{_tenant.RepositoryId}/search?q=login");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var results = await response.Content.ReadFromJsonAsync<List<SearchRow>>();
        results.Should().NotBeNull();
        results!.Select(r => r.Kind).Should().Contain(["task", "backlog"],
            "the ToLower/Contains filter has to translate into SQL for both entity types");
    }

    [Fact]
    public async Task Search_FindsAWorkItemByItsKey()
    {
        var response = await _client.GetAsync(
            $"{ApiClient.V1}/repositories/{_tenant.RepositoryId}/search?q=QRY-1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var results = await response.Content.ReadFromJsonAsync<List<SearchRow>>();
        results!.Should().Contain(r => r.Title == "Checkout flow");
    }

    [Fact]
    public async Task Search_WithATooShortTerm_ReturnsNothing()
    {
        var response = await _client.GetAsync(
            $"{ApiClient.V1}/repositories/{_tenant.RepositoryId}/search?q=a");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<List<SearchRow>>())!.Should().BeEmpty();
    }

    // ── Sprint reads: tree building and aggregation ─────────────────────────

    [Fact]
    public async Task ListingSprintTasks_BuildsTheSubTaskTree()
    {
        var response = await _client.GetAsync(
            $"{ApiClient.V1}/repositories/{_tenant.RepositoryId}/sprints/{_sprintId}/tasks");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Checkout flow");
        body.Should().Contain("Wire up the login form",
            "the sub-task is nested under its story rather than dropped");
    }

    [Fact]
    public async Task TheSprintSummaryAggregates()
    {
        var response = await _client.GetAsync(
            $"{ApiClient.V1}/repositories/{_tenant.RepositoryId}/sprints/{_sprintId}/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the working-day loop and the capacity aggregation must survive translation");
    }

    [Fact]
    public async Task ListingTheBacklogTree_Translates()
    {
        var response = await _client.GetAsync(
            $"{ApiClient.V1}/repositories/{_tenant.RepositoryId}/backlog");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Login rework");
    }

    // ── A query that would silently fall back to client evaluation ──────────

    [Fact]
    public async Task ListingBacklogItems_DoesNotLoadTheWholeTableToFilter()
    {
        // EF Core 3.0+ throws rather than evaluating a filter client-side, so reaching a result at
        // all proves the predicate translated. The assertion documents the intent.
        await using var db = database.CreateContext();

        var matches = await db.BacklogItems
            .AsNoTracking()
            .Where(b => b.RepositoryId == _tenant.RepositoryId
                     && b.Title.ToLower().Contains("login"))
            .Select(b => b.Title)
            .ToListAsync();

        matches.Should().ContainSingle().Which.Should().Be("Login rework");
    }

    private sealed record MyWorkRow(
        Guid    Id,
        Guid    RepositoryId,
        string  RepositoryCode,
        string  RepositoryName,
        Guid?   SprintId,
        string? SprintName,
        string  WorkItemNumber,
        string  Title);

    private sealed record SearchRow(string Kind, Guid Id, string Title, string? Subtitle);
}
