using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.SprintTasks.Commands.AssignSprintTask;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DASHBOARD.Tests.Application.SprintTasks.Commands;

/// <summary>Unit tests for <see cref="AssignSprintTaskCommandHandler"/>.</summary>
public sealed class AssignSprintTaskCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Mock<IHistoryService>    _history = new();
    private readonly Guid                     _repositoryId = Guid.NewGuid();
    private readonly Guid                     _sprintId     = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public AssignSprintTaskCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private AssignSprintTaskCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _history.Object, _database.Uow);

    private IRequestUserContext AuthorizedUser() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.AssignWorkItem, _repositoryId).Object;

    private async Task<SprintTask> AddTaskAsync(Guid? assignedToId = null, Guid? sprintId = null)
    {
        var task = new SprintTask
        {
            RepositoryId = _repositoryId,
            SprintId     = sprintId ?? _sprintId,
            Type         = SprintTaskType.Task,
            Title        = "Task",
            AssignedToId = assignedToId,
        };
        _db.Set<SprintTask>().Add(task);
        await _db.SaveChangesAsync();
        return task;
    }

    /// <summary>Adds a user, optionally joining them to the repository under test.</summary>
    private async Task<User> AddUserAsync(string name = "Alice", bool asMember = true)
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

        if (asMember)
        {
            _db.Set<RepositoryMember>().Add(new RepositoryMember
            {
                RepositoryId = _repositoryId,
                UserId       = user.Id,
                RoleId       = Guid.NewGuid(),
                DefaultRole  = "Developer",
            });
            await _db.SaveChangesAsync();
        }

        return user;
    }

    private Task<SprintTask> LoadAsync(Guid id) =>
        _db.Set<SprintTask>().AsNoTracking().SingleAsync(t => t.Id == id);

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutAssignWorkItemPrivilege_FailsAndLeavesTheTaskUnassigned()
    {
        var alice   = await AddUserAsync();
        var task    = await AddTaskAsync();
        var handler = CreateHandler(
            RequestUserContextMock.ForUser().AsMember(_repositoryId).Object);

        var result = await handler.Handle(
            new AssignSprintTaskCommand(_repositoryId, _sprintId, task.Id, alice.Id),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("permission");
        (await LoadAsync(task.Id)).AssignedToId.Should().BeNull();
    }

    // ── Not found ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheTaskDoesNotExist_ThrowsNotFound()
    {
        var alice   = await AddUserAsync();
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new AssignSprintTaskCommand(_repositoryId, _sprintId, Guid.NewGuid(), alice.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenTheTaskBelongsToAnotherSprint_ThrowsNotFound()
    {
        var alice   = await AddUserAsync();
        var task    = await AddTaskAsync(sprintId: Guid.NewGuid());
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new AssignSprintTaskCommand(_repositoryId, _sprintId, task.Id, alice.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>(
            "the task is looked up by sprint, not by repository");
    }

    // ── Assignee validation ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheAssigneeDoesNotExist_Fails()
    {
        var task    = await AddTaskAsync();
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new AssignSprintTaskCommand(_repositoryId, _sprintId, task.Id, Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not a member of this repository");
    }

    [Fact]
    public async Task Handle_WhenTheAssigneeIsNotARepositoryMember_Fails()
    {
        var outsider = await AddUserAsync("Outsider", asMember: false);
        var task     = await AddTaskAsync();
        var handler  = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new AssignSprintTaskCommand(_repositoryId, _sprintId, task.Id, outsider.Id),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue(
            "work may only be handed to somebody who can actually see the repository");
        (await LoadAsync(task.Id)).AssignedToId.Should().BeNull();
    }

    // ── Assignment ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AssignsTheTaskAndStampsTheTimestamp()
    {
        var alice   = await AddUserAsync();
        var task    = await AddTaskAsync();
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new AssignSprintTaskCommand(_repositoryId, _sprintId, task.Id, alice.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = await LoadAsync(task.Id);
        updated.AssignedToId.Should().Be(alice.Id);
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WithANullAssignee_UnassignsWithoutAMembershipCheck()
    {
        var alice   = await AddUserAsync();
        var task    = await AddTaskAsync(assignedToId: alice.Id);
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new AssignSprintTaskCommand(_repositoryId, _sprintId, task.Id, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await LoadAsync(task.Id)).AssignedToId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_CanReassignFromOnePersonToAnother()
    {
        var alice   = await AddUserAsync("Alice");
        var bob     = await AddUserAsync("Bob");
        var task    = await AddTaskAsync(assignedToId: alice.Id);
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new AssignSprintTaskCommand(_repositoryId, _sprintId, task.Id, bob.Id),
            CancellationToken.None);

        (await LoadAsync(task.Id)).AssignedToId.Should().Be(bob.Id);
    }

    // ── History ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_RecordsTheAssignmentByName()
    {
        var alice   = await AddUserAsync("Alice");
        var task    = await AddTaskAsync();
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new AssignSprintTaskCommand(_repositoryId, _sprintId, task.Id, alice.Id),
            CancellationToken.None);

        _history.Verify(h => h.Record(task.Id, _repositoryId, It.IsAny<Guid>(),
            "Assigned to 'Alice'."), Times.Once);
    }

    [Fact]
    public async Task Handle_RecordsUnassignment()
    {
        var alice   = await AddUserAsync();
        var task    = await AddTaskAsync(assignedToId: alice.Id);
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new AssignSprintTaskCommand(_repositoryId, _sprintId, task.Id, null),
            CancellationToken.None);

        _history.Verify(h => h.Record(task.Id, _repositoryId, It.IsAny<Guid>(), "Unassigned."),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTheAssigneeIsUnchanged_RecordsNoHistory()
    {
        var alice   = await AddUserAsync();
        var task    = await AddTaskAsync(assignedToId: alice.Id);
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new AssignSprintTaskCommand(_repositoryId, _sprintId, task.Id, alice.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue("a no-op assignment is accepted, just not logged");
        _history.Verify(h => h.Record(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<string>()), Times.Never);
    }
}
