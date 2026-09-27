using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.SprintTasks.Queries.GetBoardTasks;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Tests.Common;

namespace DASHBOARD.Tests.Application.SprintTasks.Queries;

/// <summary>Unit tests for <see cref="GetBoardTasksQueryHandler"/> — the sprint board card feed.</summary>
public sealed class GetBoardTasksQueryHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();
    private readonly Guid                     _sprintId     = Guid.NewGuid();

    /// <summary>Sets up an isolated database holding the repository under test.</summary>
    public GetBoardTasksQueryHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;

        _db.Set<Repository>().Add(
            new Repository { Name = "Dashboard", Code = "DASH" }.WithId(_repositoryId));
        _db.SaveChanges();
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private GetBoardTasksQueryHandler CreateHandler(IRequestUserContext user) => new(_db, user);

    private IRequestUserContext MemberUser() =>
        RequestUserContextMock.ForUser().AsMember(_repositoryId).Object;

    private async Task<User> AddUserAsync(string name = "Alice")
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

    private async Task<SprintTask> AddTaskAsync(
        string           title          = "Card",
        SprintTaskType   type           = SprintTaskType.Task,
        SprintTaskState  state          = SprintTaskState.Todo,
        WorkItemPriority priority       = WorkItemPriority.Medium,
        Guid?            assignedToId   = null,
        Guid?            sprintId       = null,
        int              workItemNumber = 1,
        bool             isDeleted      = false)
    {
        var task = new SprintTask
        {
            RepositoryId     = _repositoryId,
            SprintId         = sprintId ?? _sprintId,
            Type             = type,
            State            = state,
            Priority         = priority,
            Title            = title,
            AssignedToId     = assignedToId,
            WorkItemNumber   = workItemNumber,
            OriginalEstimate = 8m,
            RemainingWork    = 5m,
            CompletedWork    = 3m,
            StateChangedAt   = DateTime.UtcNow,
            IsDeleted        = isDeleted,
            DeletedAt        = isDeleted ? DateTime.UtcNow : null,
        };
        _db.Set<SprintTask>().Add(task);
        await _db.SaveChangesAsync();
        return task;
    }

    private async Task<RepositoryMetadata> AddCatalogValueAsync(
        string value, MetadataKey key = MetadataKey.Labels)
    {
        var entry = new RepositoryMetadata
        {
            RepositoryId = _repositoryId,
            IsGlobal     = false,
            Key          = key,
            Value        = value,
        };
        _db.Set<RepositoryMetadata>().Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }

    private async Task AttachMetadataAsync(Guid taskId, Guid metadataId)
    {
        _db.Set<WorkItemMetadata>().Add(new WorkItemMetadata
        {
            SprintTaskId = taskId,
            MetadataId   = metadataId,
        });
        await _db.SaveChangesAsync();
    }

    // ── Access control ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheUserIsNotAMember_Throws()
    {
        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(
            new GetBoardTasksQuery(_repositoryId, _sprintId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── Scoping ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithAnEmptySprint_ReturnsNothing()
    {
        var result = await CreateHandler(MemberUser()).Handle(
            new GetBoardTasksQuery(_repositoryId, _sprintId), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyCardsOfTheRequestedSprint()
    {
        await AddTaskAsync("In sprint",  workItemNumber: 1);
        await AddTaskAsync("Other sprint", sprintId: Guid.NewGuid(), workItemNumber: 2);

        var result = await CreateHandler(MemberUser()).Handle(
            new GetBoardTasksQuery(_repositoryId, _sprintId), CancellationToken.None);

        result.Should().ContainSingle().Which.Title.Should().Be("In sprint");
    }

    [Fact]
    public async Task Handle_ExcludesSoftDeletedCards()
    {
        await AddTaskAsync("Live",    workItemNumber: 1);
        await AddTaskAsync("Deleted", workItemNumber: 2, isDeleted: true);

        var result = await CreateHandler(MemberUser()).Handle(
            new GetBoardTasksQuery(_repositoryId, _sprintId), CancellationToken.None);

        result.Should().ContainSingle().Which.Title.Should().Be("Live",
            "a deleted card must not linger on the board");
    }

    // ── Ordering ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_OrdersByTypeThenState()
    {
        await AddTaskAsync("Task card",  SprintTaskType.Task,      SprintTaskState.Todo,   workItemNumber: 2);
        await AddTaskAsync("Story card", SprintTaskType.UserStory, SprintTaskState.Active, workItemNumber: 1);

        var result = await CreateHandler(MemberUser()).Handle(
            new GetBoardTasksQuery(_repositoryId, _sprintId), CancellationToken.None);

        // UserStory sorts before Task in the enum, so it leads regardless of state.
        result.Select(r => r.Title).Should().ContainInOrder("Story card", "Task card");
    }

    // ── Projection ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_BuildsTheWorkItemKeyFromTheRepositoryCode()
    {
        await AddTaskAsync(workItemNumber: 7);

        var result = await CreateHandler(MemberUser()).Handle(
            new GetBoardTasksQuery(_repositoryId, _sprintId), CancellationToken.None);

        result[0].WorkItemNumber.Should().Be(SprintTask.BuildWorkItemNumber("DASH", 7));
    }

    [Fact]
    public async Task Handle_CarriesTheEffortFiguresOntoTheCard()
    {
        await AddTaskAsync(priority: WorkItemPriority.Critical);

        var result = await CreateHandler(MemberUser()).Handle(
            new GetBoardTasksQuery(_repositoryId, _sprintId), CancellationToken.None);

        var card = result.Single();
        card.Priority.Should().Be(WorkItemPriority.Critical);
        card.OriginalEstimate.Should().Be(8m);
        card.RemainingWork.Should().Be(5m);
        card.CompletedWork.Should().Be(3m);
        card.StateChangedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ResolvesTheAssigneeProfile()
    {
        var alice = await AddUserAsync();
        await AddTaskAsync(assignedToId: alice.Id);

        var result = await CreateHandler(MemberUser()).Handle(
            new GetBoardTasksQuery(_repositoryId, _sprintId), CancellationToken.None);

        var card = result.Single();
        card.AssignedToId.Should().Be(alice.Id);
        card.AssignedToName.Should().Be("Alice");
        card.AssignedToAvatar.Should().Be("bg-sky-600");
    }

    [Fact]
    public async Task Handle_LeavesTheAssigneeFieldsNullForUnassignedCards()
    {
        await AddTaskAsync(assignedToId: null);

        var result = await CreateHandler(MemberUser()).Handle(
            new GetBoardTasksQuery(_repositoryId, _sprintId), CancellationToken.None);

        var card = result.Single();
        card.AssignedToId.Should().BeNull();
        card.AssignedToName.Should().BeNull();
        card.AssignedToAvatar.Should().BeNull();
    }

    // ── Label chips ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithNoLabels_ReturnsAnEmptyChipList()
    {
        await AddTaskAsync();

        var result = await CreateHandler(MemberUser()).Handle(
            new GetBoardTasksQuery(_repositoryId, _sprintId), CancellationToken.None);

        result[0].Labels.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_AttachesTheLabelChipsOfEachCard()
    {
        var task     = await AddTaskAsync();
        var urgent   = await AddCatalogValueAsync("urgent");
        var frontend = await AddCatalogValueAsync("frontend");
        await AttachMetadataAsync(task.Id, urgent.Id);
        await AttachMetadataAsync(task.Id, frontend.Id);

        var result = await CreateHandler(MemberUser()).Handle(
            new GetBoardTasksQuery(_repositoryId, _sprintId), CancellationToken.None);

        result[0].Labels.Should().BeEquivalentTo(["urgent", "frontend"]);
    }

    [Fact]
    public async Task Handle_ShowsOnlyLabelsNotEveryMetadataKey()
    {
        var task    = await AddTaskAsync();
        var label   = await AddCatalogValueAsync("urgent");
        var version = await AddCatalogValueAsync("v1.0", MetadataKey.FixedInVersion);
        await AttachMetadataAsync(task.Id, label.Id);
        await AttachMetadataAsync(task.Id, version.Id);

        var result = await CreateHandler(MemberUser()).Handle(
            new GetBoardTasksQuery(_repositoryId, _sprintId), CancellationToken.None);

        result[0].Labels.Should().ContainSingle().Which.Should().Be("urgent",
            "only the Labels catalog renders as card chips");
    }

    [Fact]
    public async Task Handle_KeepsEachCardsLabelsSeparate()
    {
        var first  = await AddTaskAsync("First",  workItemNumber: 1);
        var second = await AddTaskAsync("Second", workItemNumber: 2);
        var urgent = await AddCatalogValueAsync("urgent");
        await AttachMetadataAsync(first.Id, urgent.Id);

        var result = await CreateHandler(MemberUser()).Handle(
            new GetBoardTasksQuery(_repositoryId, _sprintId), CancellationToken.None);

        result.Single(r => r.Title == "First").Labels.Should().ContainSingle();
        result.Single(r => r.Title == "Second").Labels.Should().BeEmpty();
    }
}
