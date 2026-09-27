using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Users.Commands.SetUserManager;
using DASHBOARD.Domain.Entities;
using DASHBOARD.UnitTests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.UnitTests.Application.Users.Commands;

/// <summary>Unit tests for <see cref="SetUserManagerCommandHandler"/>, focused on cycle detection.</summary>
public sealed class SetUserManagerCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;

    /// <summary>Sets up an isolated in-memory database.</summary>
    public SetUserManagerCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private SetUserManagerCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private IRequestUserContext AdminUser() =>
        RequestUserContextMock.ForUser().AsGlobalAdmin().Object;

    private async Task<User> AddUserAsync(string name, Guid? managerId = null, bool isDeleted = false)
    {
        var user = new User
        {
            Name         = name,
            Email        = $"{name.ToLowerInvariant()}@test.local",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            ManagerId    = managerId,
            IsDeleted    = isDeleted,
            DeletedAt    = isDeleted ? DateTime.UtcNow : null,
        };
        _db.Set<User>().Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private Task<User> LoadAsync(Guid id) =>
        _db.Set<User>().IgnoreQueryFilters().AsNoTracking().SingleAsync(u => u.Id == id);

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenRequesterIsNotGlobalAdmin_Fails()
    {
        var employee = await AddUserAsync("Employee");
        var manager  = await AddUserAsync("Manager");
        var handler  = CreateHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(
            new SetUserManagerCommand(employee.Id, manager.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await LoadAsync(employee.Id)).ManagerId.Should().BeNull();
    }

    // ── Self-reference ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenManagerIsTheUserThemselves_Fails()
    {
        var employee = await AddUserAsync("Employee");
        var handler  = CreateHandler(AdminUser());

        var result = await handler.Handle(
            new SetUserManagerCommand(employee.Id, employee.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("their own manager");
    }

    // ── Existence ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ThrowsNotFound()
    {
        var manager = await AddUserAsync("Manager");
        var handler = CreateHandler(AdminUser());

        var act = () => handler.Handle(
            new SetUserManagerCommand(Guid.NewGuid(), manager.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenManagerDoesNotExist_Fails()
    {
        var employee = await AddUserAsync("Employee");
        var handler  = CreateHandler(AdminUser());

        var result = await handler.Handle(
            new SetUserManagerCommand(employee.Id, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("manager does not exist");
    }

    // ── Cycle detection ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenAssignmentWouldCreateDirectCycle_Fails()
    {
        // Alice manages Bob; making Alice report to Bob closes a two-person loop.
        var alice = await AddUserAsync("Alice");
        var bob   = await AddUserAsync("Bob", managerId: alice.Id);

        var handler = CreateHandler(AdminUser());

        var result = await handler.Handle(
            new SetUserManagerCommand(alice.Id, bob.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cycle");
        (await LoadAsync(alice.Id)).ManagerId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenAssignmentWouldCreateDeepCycle_Fails()
    {
        // A ← B ← C ← D: pointing A at D closes a four-person loop.
        var a = await AddUserAsync("A");
        var b = await AddUserAsync("B", managerId: a.Id);
        var c = await AddUserAsync("C", managerId: b.Id);
        var d = await AddUserAsync("D", managerId: c.Id);

        var handler = CreateHandler(AdminUser());

        var result = await handler.Handle(
            new SetUserManagerCommand(a.Id, d.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue("the walk must follow the whole chain, not just one hop");
        result.Error.Should().Contain("cycle");
    }

    [Fact]
    public async Task Handle_WhenManagerIsInAnUnrelatedBranch_Succeeds()
    {
        var root      = await AddUserAsync("Root");
        var branchOne = await AddUserAsync("BranchOne", managerId: root.Id);
        var employee  = await AddUserAsync("Employee");

        var handler = CreateHandler(AdminUser());

        var result = await handler.Handle(
            new SetUserManagerCommand(employee.Id, branchOne.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await LoadAsync(employee.Id)).ManagerId.Should().Be(branchOne.Id);
    }

    [Fact]
    public async Task Handle_ConsidersDeactivatedUsersWhenWalkingTheChain()
    {
        // The middle link is soft-deleted; the cycle check must still see it.
        var alice = await AddUserAsync("Alice");
        var bob   = await AddUserAsync("Bob", managerId: alice.Id, isDeleted: true);

        var handler = CreateHandler(AdminUser());

        var result = await handler.Handle(
            new SetUserManagerCommand(alice.Id, bob.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue(
            "IgnoreQueryFilters keeps deactivated users visible to the cycle walk");
        result.Error.Should().Contain("cycle");
    }

    // ── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidManager_AssignsAndStampsTimestamp()
    {
        var employee = await AddUserAsync("Employee");
        var manager  = await AddUserAsync("Manager");
        var handler  = CreateHandler(AdminUser());

        var result = await handler.Handle(
            new SetUserManagerCommand(employee.Id, manager.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = await LoadAsync(employee.Id);
        updated.ManagerId.Should().Be(manager.Id);
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WithNullManager_ClearsTheManagerAndSkipsCycleCheck()
    {
        var manager  = await AddUserAsync("Manager");
        var employee = await AddUserAsync("Employee", managerId: manager.Id);
        var handler  = CreateHandler(AdminUser());

        var result = await handler.Handle(
            new SetUserManagerCommand(employee.Id, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await LoadAsync(employee.Id)).ManagerId.Should().BeNull("the user becomes a hierarchy root");
    }

    [Fact]
    public async Task Handle_CanReassignAUserToADifferentManager()
    {
        var first    = await AddUserAsync("First");
        var second   = await AddUserAsync("Second");
        var employee = await AddUserAsync("Employee", managerId: first.Id);
        var handler  = CreateHandler(AdminUser());

        var result = await handler.Handle(
            new SetUserManagerCommand(employee.Id, second.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await LoadAsync(employee.Id)).ManagerId.Should().Be(second.Id);
    }

    [Fact]
    public async Task Handle_AllowsAssigningAManagerWhoIsADeactivatedUser()
    {
        var manager  = await AddUserAsync("Manager", isDeleted: true);
        var employee = await AddUserAsync("Employee");
        var handler  = CreateHandler(AdminUser());

        var result = await handler.Handle(
            new SetUserManagerCommand(employee.Id, manager.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(
            "the existence check runs with IgnoreQueryFilters, so deactivated users are still valid targets");
    }
}
