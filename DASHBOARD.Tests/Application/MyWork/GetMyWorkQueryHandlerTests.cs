using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.MyWork.Queries.GetMyWork;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Tests.Common;

namespace DASHBOARD.Tests.Application.MyWork;

/// <summary>Unit tests for <see cref="GetMyWorkQueryHandler"/>.</summary>
public sealed class GetMyWorkQueryHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _userId       = Guid.NewGuid();
    private readonly Guid                     _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated database with one repository the user belongs to.</summary>
    public GetMyWorkQueryHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;

        _db.Set<Repository>().Add(
            new Repository { Name = "Dashboard", Code = "DASH" }.WithId(_repositoryId));
        _db.SaveChanges();
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private GetMyWorkQueryHandler CreateHandler() =>
        new(_db, RequestUserContextMock.ForUser(_userId).Object);

    private async Task AddMembershipAsync(Guid? repositoryId = null, Guid? userId = null)
    {
        _db.Set<RepositoryMember>().Add(new RepositoryMember
        {
            RepositoryId = repositoryId ?? _repositoryId,
            UserId       = userId ?? _userId,
            RoleId       = Guid.NewGuid(),
            DefaultRole  = "Developer",
        });
        await _db.SaveChangesAsync();
    }

    private async Task<Repository> AddRepositoryAsync(
        string code, bool isArchived = false, Guid? id = null)
    {
        var repo = new Repository { Name = code, Code = code, IsArchived = isArchived };
        if (id.HasValue) repo.WithId(id.Value);

        _db.Set<Repository>().Add(repo);
        await _db.SaveChangesAsync();
        return repo;
    }

    private async Task<SprintTask> AddTaskAsync(
        Guid?            assignedToId   = null,
        SprintTaskState  state          = SprintTaskState.Active,
        WorkItemPriority priority       = WorkItemPriority.Medium,
        Guid?            repositoryId   = null,
        Guid?            sprintId       = null,
        int              workItemNumber = 1,
        string           title          = "My task")
    {
        var task = new SprintTask
        {
            RepositoryId   = repositoryId ?? _repositoryId,
            SprintId       = sprintId,
            Type           = SprintTaskType.Task,
            Title          = title,
            AssignedToId   = assignedToId ?? _userId,
            State          = state,
            Priority       = priority,
            WorkItemNumber = workItemNumber,
            StoryPoints    = 3,
            RemainingWork  = 4m,
        };
        _db.Set<SprintTask>().Add(task);
        await _db.SaveChangesAsync();
        return task;
    }

    // ── Visibility boundary ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheUserBelongsToNoRepository_ReturnsNothing()
    {
        await AddTaskAsync();

        var result = await CreateHandler().Handle(new GetMyWorkQuery(), CancellationToken.None);

        result.Should().BeEmpty("membership is the visibility boundary");
    }

    [Fact]
    public async Task Handle_ExcludesTasksInRepositoriesTheUserHasLeft()
    {
        await AddMembershipAsync();
        var otherRepo = await AddRepositoryAsync("OTHER");
        await AddTaskAsync(repositoryId: otherRepo.Id);

        var result = await CreateHandler().Handle(new GetMyWorkQuery(), CancellationToken.None);

        result.Should().BeEmpty(
            "a task assigned in a repository the user is no longer a member of stays hidden");
    }

    [Fact]
    public async Task Handle_ExcludesArchivedRepositories()
    {
        var archived = await AddRepositoryAsync("OLD", isArchived: true);
        await AddMembershipAsync(repositoryId: archived.Id);
        await AddTaskAsync(repositoryId: archived.Id);

        var result = await CreateHandler().Handle(new GetMyWorkQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    // ── Assignment and state filters ────────────────────────────────────────

    [Fact]
    public async Task Handle_ExcludesTasksAssignedToSomebodyElse()
    {
        await AddMembershipAsync();
        await AddTaskAsync(assignedToId: Guid.NewGuid());

        var result = await CreateHandler().Handle(new GetMyWorkQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ExcludesUnassignedTasks()
    {
        await AddMembershipAsync();
        var task = await AddTaskAsync();
        task.AssignedToId = null;
        await _db.SaveChangesAsync();

        var result = await CreateHandler().Handle(new GetMyWorkQuery(), CancellationToken.None);

        result.Should().BeEmpty("'my work' means work someone put my name on");
    }

    [Fact]
    public async Task Handle_ExcludesFinishedWork()
    {
        await AddMembershipAsync();
        await AddTaskAsync(state: SprintTaskState.Done);

        var result = await CreateHandler().Handle(new GetMyWorkQuery(), CancellationToken.None);

        result.Should().BeEmpty("a Done item is no longer outstanding work");
    }

    [Theory]
    [InlineData(SprintTaskState.New)]
    [InlineData(SprintTaskState.Backlog)]
    [InlineData(SprintTaskState.Todo)]
    [InlineData(SprintTaskState.Active)]
    [InlineData(SprintTaskState.InReview)]
    public async Task Handle_IncludesEveryUnfinishedState(SprintTaskState state)
    {
        await AddMembershipAsync();
        await AddTaskAsync(state: state);

        var result = await CreateHandler().Handle(new GetMyWorkQuery(), CancellationToken.None);

        result.Should().ContainSingle();
    }

    // ── Projection ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ProjectsTheRepositoryAndWorkItemKey()
    {
        await AddMembershipAsync();
        var task = await AddTaskAsync(workItemNumber: 42, title: "Ship it");

        var result = await CreateHandler().Handle(new GetMyWorkQuery(), CancellationToken.None);

        var item = result.Should().ContainSingle().Subject;
        item.Id.Should().Be(task.Id);
        item.RepositoryId.Should().Be(_repositoryId);
        item.RepositoryCode.Should().Be("DASH");
        item.RepositoryName.Should().Be("Dashboard");
        item.WorkItemNumber.Should().Be(SprintTask.BuildWorkItemNumber("DASH", 42));
        item.Title.Should().Be("Ship it");
        item.StoryPoints.Should().Be(3);
        item.RemainingWork.Should().Be(4m);
    }

    [Fact]
    public async Task Handle_ResolvesTheSprintNameWhenTheTaskIsInASprint()
    {
        await AddMembershipAsync();

        var sprintId = Guid.NewGuid();
        _db.Set<Sprint>().Add(new Sprint
        {
            RepositoryId = _repositoryId,
            Name         = "Sprint 7",
            StartDate    = new DateOnly(2026, 5, 4),
            EndDate      = new DateOnly(2026, 5, 15),
        }.WithId(sprintId));
        await _db.SaveChangesAsync();

        await AddTaskAsync(sprintId: sprintId);

        var result = await CreateHandler().Handle(new GetMyWorkQuery(), CancellationToken.None);

        result[0].SprintId.Should().Be(sprintId);
        result[0].SprintName.Should().Be("Sprint 7");
    }

    [Fact]
    public async Task Handle_KeepsStandaloneTasksThatBelongToNoSprint()
    {
        await AddMembershipAsync();
        await AddTaskAsync(sprintId: null);

        var result = await CreateHandler().Handle(new GetMyWorkQuery(), CancellationToken.None);

        result.Should().ContainSingle("the left join must not drop unscheduled work");
        result[0].SprintId.Should().BeNull();
        result[0].SprintName.Should().BeNull();
    }

    // ── Ordering ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_OrdersByPriorityDescendingFirst()
    {
        await AddMembershipAsync();
        await AddTaskAsync(priority: WorkItemPriority.Low,      workItemNumber: 1, title: "Low");
        await AddTaskAsync(priority: WorkItemPriority.Critical, workItemNumber: 2, title: "Critical");
        await AddTaskAsync(priority: WorkItemPriority.Medium,   workItemNumber: 3, title: "Medium");

        var result = await CreateHandler().Handle(new GetMyWorkQuery(), CancellationToken.None);

        result.Select(r => r.Title).Should().ContainInOrder("Critical", "Medium", "Low");
    }

    [Fact]
    public async Task Handle_GathersWorkFromEveryRepositoryTheUserBelongsTo()
    {
        await AddMembershipAsync();
        var second = await AddRepositoryAsync("SEC");
        await AddMembershipAsync(repositoryId: second.Id);

        await AddTaskAsync(title: "In DASH");
        await AddTaskAsync(repositoryId: second.Id, title: "In SEC");

        var result = await CreateHandler().Handle(new GetMyWorkQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(r => r.RepositoryCode).Should().Contain(["DASH", "SEC"]);
    }
}
