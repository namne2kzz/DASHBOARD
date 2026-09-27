using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Search.Queries.GlobalSearch;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Tests.Common;

namespace DASHBOARD.Tests.Application.Search;

/// <summary>Unit tests for <see cref="GlobalSearchQueryHandler"/>.</summary>
public sealed class GlobalSearchQueryHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated database holding the searched repository.</summary>
    public GlobalSearchQueryHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;

        _db.Set<Repository>().Add(
            new Repository { Name = "Dashboard", Code = "DASH" }.WithId(_repositoryId));
        _db.SaveChanges();
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private GlobalSearchQueryHandler CreateHandler(IRequestUserContext user) => new(_db, user);

    private IRequestUserContext MemberUser() =>
        RequestUserContextMock.ForUser().AsMember(_repositoryId).Object;

    private async Task<SprintTask> AddTaskAsync(
        string    title,
        string    description        = "",
        string?   acceptanceCriteria = null,
        int       workItemNumber     = 1,
        DateTime? stateChangedAt     = null,
        Guid?     repositoryId       = null)
    {
        var task = new SprintTask
        {
            RepositoryId       = repositoryId ?? _repositoryId,
            Type               = SprintTaskType.Task,
            Title              = title,
            Description        = description,
            AcceptanceCriteria = acceptanceCriteria,
            WorkItemNumber     = workItemNumber,
            StateChangedAt     = stateChangedAt ?? DateTime.UtcNow,
        };
        _db.Set<SprintTask>().Add(task);
        await _db.SaveChangesAsync();
        return task;
    }

    private async Task<BacklogItem> AddBacklogAsync(
        string title, decimal rank = 1000m, Guid? repositoryId = null)
    {
        var item = new BacklogItem
        {
            RepositoryId = repositoryId ?? _repositoryId,
            Type         = BacklogItemType.UserStory,
            State        = BacklogItemState.New,
            Title        = title,
            Rank         = rank,
        };
        _db.Set<BacklogItem>().Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    // ── Access control ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheUserIsNotAMember_Throws()
    {
        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(
            new GlobalSearchQuery(_repositoryId, "login"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── Minimum term length ─────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("  a  ")]
    public async Task Handle_WithATermShorterThanTwoCharacters_ReturnsNothing(string term)
    {
        await AddTaskAsync("Anything");

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new GlobalSearchQuery(_repositoryId, term), CancellationToken.None);

        result.Should().BeEmpty("a one-letter search would match almost everything");
    }

    // ── Matching sprint tasks ───────────────────────────────────────────────

    [Fact]
    public async Task Handle_MatchesTaskTitles()
    {
        await AddTaskAsync("Fix the login page");
        await AddTaskAsync("Unrelated work");

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new GlobalSearchQuery(_repositoryId, "login"), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Kind.Should().Be("task");
        result[0].Title.Should().Be("Fix the login page");
    }

    [Fact]
    public async Task Handle_MatchesTaskDescriptionsAndAcceptanceCriteria()
    {
        await AddTaskAsync("Opaque title", description: "Touches the login flow");
        await AddTaskAsync("Another title", workItemNumber: 2,
            acceptanceCriteria: "Given a login attempt…");

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new GlobalSearchQuery(_repositoryId, "login"), CancellationToken.None);

        result.Should().HaveCount(2, "search looks past the title into the body");
    }

    [Theory]
    [InlineData("LOGIN")]
    [InlineData("Login")]
    [InlineData("  login  ")]
    public async Task Handle_MatchingIsCaseInsensitiveAndTrimmed(string term)
    {
        await AddTaskAsync("Fix the login page");

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new GlobalSearchQuery(_repositoryId, term), CancellationToken.None);

        result.Should().ContainSingle();
    }

    // ── Work-item number lookup ─────────────────────────────────────────────

    [Theory]
    // the bare number
    [InlineData("34")]
    // the full work-item key
    [InlineData("DASH-34")]
    // lower case key
    [InlineData("dash-34")]
    public async Task Handle_FindsATaskByItsWorkItemNumber(string term)
    {
        await AddTaskAsync("Some unrelated title", workItemNumber: 34);

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new GlobalSearchQuery(_repositoryId, term), CancellationToken.None);

        result.Should().ContainSingle("the number is extracted from the search term");
        result[0].Subtitle.Should().Contain("DASH-34");
    }

    [Fact]
    public async Task Handle_DoesNotMatchADifferentWorkItemNumber()
    {
        await AddTaskAsync("Some title", workItemNumber: 35);

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new GlobalSearchQuery(_repositoryId, "DASH-34"), CancellationToken.None);

        result.Should().BeEmpty();
    }

    // ── Matching backlog items ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_MatchesBacklogTitles()
    {
        await AddBacklogAsync("Login rework");

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new GlobalSearchQuery(_repositoryId, "login"), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Kind.Should().Be("backlog");
        result[0].Subtitle.Should().Be(BacklogItemType.UserStory.ToString());
    }

    [Fact]
    public async Task Handle_ReturnsBothKindsTogether()
    {
        await AddTaskAsync("Login bug");
        await AddBacklogAsync("Login rework");

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new GlobalSearchQuery(_repositoryId, "login"), CancellationToken.None);

        result.Select(r => r.Kind).Should().Contain(["task", "backlog"]);
    }

    // ── Scoping ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_IgnoresTasksAndBacklogOfOtherRepositories()
    {
        var foreignRepo = Guid.NewGuid();
        await AddTaskAsync("Login bug",     repositoryId: foreignRepo);
        await AddBacklogAsync("Login work", repositoryId: foreignRepo);

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new GlobalSearchQuery(_repositoryId, "login"), CancellationToken.None);

        result.Should().BeEmpty("search never crosses a repository boundary");
    }

    // ── Result limit ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CapsEachKindAtSixResults()
    {
        for (var i = 1; i <= 8; i++)
            await AddTaskAsync($"Login issue {i}", workItemNumber: i);
        for (var i = 0; i < 8; i++)
            await AddBacklogAsync($"Login story {i}", rank: (i + 1) * 1000m);

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new GlobalSearchQuery(_repositoryId, "login"), CancellationToken.None);

        result.Count(r => r.Kind == "task").Should().Be(6);
        result.Count(r => r.Kind == "backlog").Should().Be(6);
    }

    [Fact]
    public async Task Handle_ReturnsTheMostRecentlyMovedTasksFirst()
    {
        var now = DateTime.UtcNow;
        await AddTaskAsync("Login old",    workItemNumber: 1, stateChangedAt: now.AddDays(-5));
        await AddTaskAsync("Login recent", workItemNumber: 2, stateChangedAt: now);

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(
            new GlobalSearchQuery(_repositoryId, "login"), CancellationToken.None);

        result.First(r => r.Kind == "task").Title.Should().Be("Login recent");
    }
}
