using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Users.Queries.GetUserHierarchy;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Tests.Common;

namespace DASHBOARD.Tests.Application.Users.Queries;

/// <summary>Unit tests for <see cref="GetUserHierarchyQueryHandler"/>.</summary>
public sealed class GetUserHierarchyQueryHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;

    /// <summary>Sets up an isolated in-memory database.</summary>
    public GetUserHierarchyQueryHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private GetUserHierarchyQueryHandler CreateHandler(IRequestUserContext user) => new(_db, user);

    private IRequestUserContext AdminUser() =>
        RequestUserContextMock.ForUser().AsGlobalAdmin().Object;

    private async Task<User> AddUserAsync(
        string name, Guid? managerId = null, bool isDeleted = false, bool isGlobalAdmin = false)
    {
        var user = new User
        {
            Name          = name,
            Email         = $"{name.ToLowerInvariant()}@test.local",
            PasswordHash  = "hash",
            PasswordSalt  = "salt",
            ManagerId     = managerId,
            IsGlobalAdmin = isGlobalAdmin,
            IsDeleted     = isDeleted,
            DeletedAt     = isDeleted ? DateTime.UtcNow : null,
        };
        _db.Set<User>().Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenRequesterIsNotGlobalAdmin_Throws()
    {
        var target  = await AddUserAsync("Target");
        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(new GetUserHierarchyQuery(target.Id), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_WhenFocusUserDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(AdminUser());

        var act = () => handler.Handle(new GetUserHierarchyQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Root user ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ForARootUser_ReturnsNoManagerAndNoAncestors()
    {
        var root    = await AddUserAsync("Root");
        var handler = CreateHandler(AdminUser());

        var result = await handler.Handle(new GetUserHierarchyQuery(root.Id), CancellationToken.None);

        result.Manager.Should().BeNull();
        result.Ancestors.Should().BeEmpty();
        result.Self.Id.Should().Be(root.Id);
    }

    // ── Ancestors ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsAncestorsInTopDownOrder()
    {
        // CEO ← Director ← Lead ← Employee
        var ceo      = await AddUserAsync("CEO");
        var director = await AddUserAsync("Director", managerId: ceo.Id);
        var lead     = await AddUserAsync("Lead",     managerId: director.Id);
        var employee = await AddUserAsync("Employee", managerId: lead.Id);

        var handler = CreateHandler(AdminUser());

        var result = await handler.Handle(new GetUserHierarchyQuery(employee.Id), CancellationToken.None);

        result.Ancestors.Select(a => a.Name)
              .Should().ContainInOrder("CEO", "Director", "Lead");
        result.Manager!.Name.Should().Be("Lead", "the direct manager is the last ancestor");
    }

    [Fact]
    public async Task Handle_WhenTheChainContainsACycle_StopsInsteadOfHanging()
    {
        // Data corruption: Alice ↔ Bob manage each other. The walk must terminate.
        var alice = await AddUserAsync("Alice");
        var bob   = await AddUserAsync("Bob", managerId: alice.Id);

        alice.ManagerId = bob.Id;
        await _db.SaveChangesAsync();

        var handler = CreateHandler(AdminUser());

        var result = await handler.Handle(new GetUserHierarchyQuery(alice.Id), CancellationToken.None);

        result.Ancestors.Should().HaveCountLessThan(101,
            "the guard caps the walk at 100 hops rather than looping forever");
    }

    // ── Peers ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsPeersSharingTheSameManagerExcludingSelf()
    {
        var manager = await AddUserAsync("Manager");
        var self    = await AddUserAsync("Self",    managerId: manager.Id);
        await AddUserAsync("PeerB",   managerId: manager.Id);
        await AddUserAsync("PeerA",   managerId: manager.Id);
        await AddUserAsync("Outsider");

        var handler = CreateHandler(AdminUser());

        var result = await handler.Handle(new GetUserHierarchyQuery(self.Id), CancellationToken.None);

        result.Peers.Select(p => p.Name).Should().ContainInOrder("PeerA", "PeerB");
        result.Peers.Should().HaveCount(2);
        result.Peers.Should().NotContain(p => p.Id == self.Id);
    }

    [Fact]
    public async Task Handle_ForRootUsers_TreatsOtherRootsAsPeers()
    {
        var self = await AddUserAsync("SelfRoot");
        await AddUserAsync("OtherRoot");

        var handler = CreateHandler(AdminUser());

        var result = await handler.Handle(new GetUserHierarchyQuery(self.Id), CancellationToken.None);

        result.Peers.Should().ContainSingle(p => p.Name == "OtherRoot",
            "users with a null manager share the same (null) manager");
    }

    // ── Subordinates ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsDirectReportsOrderedByName()
    {
        var manager = await AddUserAsync("Manager");
        await AddUserAsync("Zach",  managerId: manager.Id);
        await AddUserAsync("Alice", managerId: manager.Id);

        var handler = CreateHandler(AdminUser());

        var result = await handler.Handle(new GetUserHierarchyQuery(manager.Id), CancellationToken.None);

        result.Subordinates.Select(s => s.Name).Should().ContainInOrder("Alice", "Zach");
    }

    [Fact]
    public async Task Handle_DoesNotIncludeIndirectReportsAsSubordinates()
    {
        var manager  = await AddUserAsync("Manager");
        var lead     = await AddUserAsync("Lead",     managerId: manager.Id);
        await AddUserAsync("Grandchild", managerId: lead.Id);

        var handler = CreateHandler(AdminUser());

        var result = await handler.Handle(new GetUserHierarchyQuery(manager.Id), CancellationToken.None);

        result.Subordinates.Should().ContainSingle(s => s.Name == "Lead",
            "only direct reports belong here");
    }

    // ── Node projection ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReportsSubordinateCountPerNode()
    {
        var manager = await AddUserAsync("Manager");
        var lead    = await AddUserAsync("Lead", managerId: manager.Id);
        await AddUserAsync("ReportA", managerId: lead.Id);
        await AddUserAsync("ReportB", managerId: lead.Id);

        var handler = CreateHandler(AdminUser());

        var result = await handler.Handle(new GetUserHierarchyQuery(manager.Id), CancellationToken.None);

        result.Self.SubordinateCount.Should().Be(1);
        result.Subordinates.Single(s => s.Name == "Lead").SubordinateCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_IncludesDeactivatedUsersAndMarksThemInactive()
    {
        var manager = await AddUserAsync("Manager");
        await AddUserAsync("Gone", managerId: manager.Id, isDeleted: true);

        var handler = CreateHandler(AdminUser());

        var result = await handler.Handle(new GetUserHierarchyQuery(manager.Id), CancellationToken.None);

        var gone = result.Subordinates.Single(s => s.Name == "Gone");
        gone.IsActive.Should().BeFalse(
            "the org chart keeps deactivated people visible so their reports are not orphaned silently");
    }

    [Fact]
    public async Task Handle_ProjectsIdentityFieldsOntoTheNode()
    {
        var self    = await AddUserAsync("Self", isGlobalAdmin: true);
        var handler = CreateHandler(AdminUser());

        var result = await handler.Handle(new GetUserHierarchyQuery(self.Id), CancellationToken.None);

        result.Self.Name.Should().Be("Self");
        result.Self.Email.Should().Be("self@test.local");
        result.Self.IsGlobalAdmin.Should().BeTrue();
        result.Self.IsActive.Should().BeTrue();
    }
}
