using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.SprintTasks.DTOs;
using DASHBOARD.Application.SprintTasks.Queries.GetSprintTaskDetail;
using DASHBOARD.Application.SprintTasks.Queries.ListSprintTasks;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Tests.Common;

namespace DASHBOARD.Tests.Application.SprintTasks.Queries;

/// <summary>
/// Unit tests for <see cref="ListSprintTasksQueryHandler"/> (the sprint work-item tree) and
/// <see cref="GetSprintTaskDetailQueryHandler"/> (a single item with its parent breadcrumb).
/// </summary>
public sealed class ListAndDetailQueryTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();
    private readonly Guid                     _sprintId     = Guid.NewGuid();

    /// <summary>Sets up an isolated database holding the repository under test.</summary>
    public ListAndDetailQueryTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;

        _db.Set<Repository>().Add(
            new Repository { Name = "Dashboard", Code = "DASH" }.WithId(_repositoryId));
        _db.SaveChanges();
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private ListSprintTasksQueryHandler ListHandler(IRequestUserContext user) => new(_db, user);

    private GetSprintTaskDetailQueryHandler DetailHandler(IRequestUserContext user) => new(_db, user);

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
        string          title          = "Work item",
        SprintTaskType  type           = SprintTaskType.Task,
        Guid?           parentId       = null,
        Guid?           assignedToId   = null,
        Guid?           sprintId       = null,
        int             workItemNumber = 1,
        bool            isDeleted      = false,
        Guid?           repositoryId   = null)
    {
        var task = new SprintTask
        {
            RepositoryId   = repositoryId ?? _repositoryId,
            SprintId       = sprintId ?? _sprintId,
            Type           = type,
            Title          = title,
            ParentId       = parentId,
            AssignedToId   = assignedToId,
            WorkItemNumber = workItemNumber,
            StoryPoints    = 5,
            IsDeleted      = isDeleted,
            DeletedAt      = isDeleted ? DateTime.UtcNow : null,
        };
        _db.Set<SprintTask>().Add(task);
        await _db.SaveChangesAsync();
        return task;
    }

    private static IEnumerable<SprintTaskDto> Flatten(IReadOnlyList<SprintTaskDto> items)
    {
        foreach (var item in items)
        {
            yield return item;
            foreach (var child in Flatten(item.SubTasks))
                yield return child;
        }
    }

    // ── ListSprintTasks: access control and scoping ─────────────────────────

    [Fact]
    public async Task List_WhenTheUserIsNotAMember_Throws()
    {
        var handler = ListHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(
            new ListSprintTasksQuery(_repositoryId, _sprintId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task List_WithAnEmptySprint_ReturnsNothing()
    {
        var result = await ListHandler(MemberUser()).Handle(
            new ListSprintTasksQuery(_repositoryId, _sprintId), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task List_ReturnsOnlyItemsOfTheRequestedSprint()
    {
        await AddTaskAsync("In sprint",    workItemNumber: 1);
        await AddTaskAsync("Other sprint", sprintId: Guid.NewGuid(), workItemNumber: 2);

        var result = await ListHandler(MemberUser()).Handle(
            new ListSprintTasksQuery(_repositoryId, _sprintId), CancellationToken.None);

        result.Should().ContainSingle().Which.Title.Should().Be("In sprint");
    }

    [Fact]
    public async Task List_ExcludesSoftDeletedItems()
    {
        await AddTaskAsync("Live",    workItemNumber: 1);
        await AddTaskAsync("Deleted", workItemNumber: 2, isDeleted: true);

        var result = await ListHandler(MemberUser()).Handle(
            new ListSprintTasksQuery(_repositoryId, _sprintId), CancellationToken.None);

        Flatten(result).Should().ContainSingle().Which.Title.Should().Be("Live");
    }

    // ── ListSprintTasks: tree building ──────────────────────────────────────

    [Fact]
    public async Task List_NestsSubTasksUnderTheirStory()
    {
        var story = await AddTaskAsync("Story", SprintTaskType.UserStory, workItemNumber: 1);
        await AddTaskAsync("Sub A", parentId: story.Id, workItemNumber: 2);
        await AddTaskAsync("Sub B", parentId: story.Id, workItemNumber: 3);

        var result = await ListHandler(MemberUser()).Handle(
            new ListSprintTasksQuery(_repositoryId, _sprintId), CancellationToken.None);

        result.Should().ContainSingle("only root items appear at the top level");
        result[0].SubTasks.Select(s => s.Title).Should().ContainInOrder("Sub A", "Sub B");
    }

    [Fact]
    public async Task List_WhenASubTaskIsDeleted_TheStoryKeepsTheRest()
    {
        var story = await AddTaskAsync("Story", SprintTaskType.UserStory, workItemNumber: 1);
        await AddTaskAsync("Live sub",    parentId: story.Id, workItemNumber: 2);
        await AddTaskAsync("Deleted sub", parentId: story.Id, workItemNumber: 3, isDeleted: true);

        var result = await ListHandler(MemberUser()).Handle(
            new ListSprintTasksQuery(_repositoryId, _sprintId), CancellationToken.None);

        result[0].SubTasks.Should().ContainSingle().Which.Title.Should().Be("Live sub");
    }

    [Fact]
    public async Task List_OrdersRootItemsByCreationOrder()
    {
        await AddTaskAsync("First",  workItemNumber: 1);
        await AddTaskAsync("Second", workItemNumber: 2);
        await AddTaskAsync("Third",  workItemNumber: 3);

        var result = await ListHandler(MemberUser()).Handle(
            new ListSprintTasksQuery(_repositoryId, _sprintId), CancellationToken.None);

        result.Select(r => r.Title).Should().ContainInOrder("First", "Second", "Third");
    }

    // ── ListSprintTasks: projection ─────────────────────────────────────────

    [Fact]
    public async Task List_ProjectsTheWorkItemKeyAndAssignee()
    {
        var alice = await AddUserAsync();
        await AddTaskAsync(assignedToId: alice.Id, workItemNumber: 7);

        var result = await ListHandler(MemberUser()).Handle(
            new ListSprintTasksQuery(_repositoryId, _sprintId), CancellationToken.None);

        var item = result.Single();
        item.WorkItemNumber.Should().Be(SprintTask.BuildWorkItemNumber("DASH", 7));
        item.AssignedToId.Should().Be(alice.Id);
        item.AssignedToName.Should().Be("Alice");
        item.AssignedToAvatar.Should().Be("bg-sky-600");
        item.StoryPoints.Should().Be(5);
    }

    [Fact]
    public async Task List_LeavesTheParentBreadcrumbEmpty()
    {
        var story = await AddTaskAsync("Story", SprintTaskType.UserStory, workItemNumber: 1);
        await AddTaskAsync("Sub", parentId: story.Id, workItemNumber: 2);

        var result = await ListHandler(MemberUser()).Handle(
            new ListSprintTasksQuery(_repositoryId, _sprintId), CancellationToken.None);

        var sub = result[0].SubTasks.Single();
        sub.ParentId.Should().Be(story.Id);
        sub.ParentWorkItemNumber.Should().BeNull(
            "nesting already shows the parent here — the breadcrumb is for the detail view");
    }

    // ── GetSprintTaskDetail ─────────────────────────────────────────────────

    [Fact]
    public async Task Detail_WhenTheUserIsNotAMember_Throws()
    {
        var task    = await AddTaskAsync();
        var handler = DetailHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(
            new GetSprintTaskDetailQuery(_repositoryId, task.Id), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Detail_WhenTheItemDoesNotExist_ThrowsNotFound()
    {
        var handler = DetailHandler(MemberUser());

        var act = () => handler.Handle(
            new GetSprintTaskDetailQuery(_repositoryId, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Detail_WhenTheItemBelongsToAnotherRepository_ThrowsNotFound()
    {
        var foreign = await AddTaskAsync(repositoryId: Guid.NewGuid());
        var handler = DetailHandler(MemberUser());

        var act = () => handler.Handle(
            new GetSprintTaskDetailQuery(_repositoryId, foreign.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Detail_WhenTheItemIsDeleted_ThrowsNotFound()
    {
        var task    = await AddTaskAsync(isDeleted: true);
        var handler = DetailHandler(MemberUser());

        var act = () => handler.Handle(
            new GetSprintTaskDetailQuery(_repositoryId, task.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>(
            "the soft-delete filter hides it from the detail view too");
    }

    [Fact]
    public async Task Detail_ReturnsTheItemWithItsWorkItemKey()
    {
        var alice = await AddUserAsync();
        var task  = await AddTaskAsync(assignedToId: alice.Id, workItemNumber: 7);

        var result = await DetailHandler(MemberUser()).Handle(
            new GetSprintTaskDetailQuery(_repositoryId, task.Id), CancellationToken.None);

        result.Id.Should().Be(task.Id);
        result.WorkItemNumber.Should().Be(SprintTask.BuildWorkItemNumber("DASH", 7));
        result.AssignedToName.Should().Be("Alice");
    }

    [Fact]
    public async Task Detail_ForARootItem_LeavesTheParentBreadcrumbNull()
    {
        var task = await AddTaskAsync();

        var result = await DetailHandler(MemberUser()).Handle(
            new GetSprintTaskDetailQuery(_repositoryId, task.Id), CancellationToken.None);

        result.ParentId.Should().BeNull();
        result.ParentWorkItemNumber.Should().BeNull();
        result.ParentTitle.Should().BeNull();
    }

    [Fact]
    public async Task Detail_ForASubTask_FillsTheParentBreadcrumb()
    {
        var story = await AddTaskAsync("Checkout flow", SprintTaskType.UserStory, workItemNumber: 3);
        var sub   = await AddTaskAsync("Sub", parentId: story.Id, workItemNumber: 4);

        var result = await DetailHandler(MemberUser()).Handle(
            new GetSprintTaskDetailQuery(_repositoryId, sub.Id), CancellationToken.None);

        result.ParentId.Should().Be(story.Id);
        result.ParentWorkItemNumber.Should().Be(SprintTask.BuildWorkItemNumber("DASH", 3));
        result.ParentTitle.Should().Be("Checkout flow");
    }

    [Fact]
    public async Task Detail_DoesNotLoadTheSubTaskTree()
    {
        var story = await AddTaskAsync("Story", SprintTaskType.UserStory, workItemNumber: 1);
        await AddTaskAsync("Sub", parentId: story.Id, workItemNumber: 2);

        var result = await DetailHandler(MemberUser()).Handle(
            new GetSprintTaskDetailQuery(_repositoryId, story.Id), CancellationToken.None);

        result.SubTasks.Should().BeEmpty(
            "the detail view fetches children separately rather than nesting them here");
    }
}
