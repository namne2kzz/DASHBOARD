using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Members.Queries.ListMembers;
using DASHBOARD.Application.Roles.Commands.DeleteRole;
using DASHBOARD.Application.Roles.Queries.ListRoles;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Tests.Application.Roles;

/// <summary>
/// Unit tests for <see cref="DeleteRoleCommandHandler"/>, <see cref="ListRolesQueryHandler"/>
/// and <see cref="ListMembersQueryHandler"/>.
/// </summary>
public sealed class RoleAndMemberListingTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public RoleAndMemberListingTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private DeleteRoleCommandHandler DeleteHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private ListRolesQueryHandler RolesHandler(IRequestUserContext user) => new(_db, user);

    private ListMembersQueryHandler MembersHandler(IRequestUserContext user) => new(_db, user);

    private IRequestUserContext RoleManager() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.ManageRoles, _repositoryId).Object;

    private IRequestUserContext MemberUser() =>
        RequestUserContextMock.ForUser().AsMember(_repositoryId).Object;

    private async Task<Role> AddRoleAsync(
        string name         = "Custom",
        bool   isDefault    = false,
        Guid?  repositoryId = null,
        params SystemFunction[] functions)
    {
        var role = new Role
        {
            Name             = name,
            Description      = $"{name} description",
            IsDefault        = isDefault,
            RepositoryId     = repositoryId ?? _repositoryId,
            AllowedFunctions = functions.Length > 0 ? [.. functions] : [SystemFunction.ViewRepository],
        };
        _db.Set<Role>().Add(role);
        await _db.SaveChangesAsync();
        return role;
    }

    private async Task<User> AddUserAsync(string name)
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

    private async Task<RepositoryMember> AddMemberAsync(
        Guid roleId, Guid userId, string defaultRole = "Developer", Guid? repositoryId = null)
    {
        var member = new RepositoryMember
        {
            RepositoryId = repositoryId ?? _repositoryId,
            UserId       = userId,
            RoleId       = roleId,
            DefaultRole  = defaultRole,
        };
        _db.Set<RepositoryMember>().Add(member);
        await _db.SaveChangesAsync();
        return member;
    }

    // ── DeleteRole: permission ──────────────────────────────────────────────

    [Fact]
    public async Task Delete_WithoutManageRolesPrivilege_FailsAndKeepsTheRole()
    {
        var role = await AddRoleAsync();

        var result = await DeleteHandler(MemberUser()).Handle(
            new DeleteRoleCommand(_repositoryId, role.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("permission");
        (await _db.Set<Role>().CountAsync()).Should().Be(1);
    }

    // ── DeleteRole: not found ───────────────────────────────────────────────

    [Fact]
    public async Task Delete_WhenTheRoleDoesNotExist_ThrowsNotFound()
    {
        var act = () => DeleteHandler(RoleManager()).Handle(
            new DeleteRoleCommand(_repositoryId, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Delete_WhenTheRoleBelongsToAnotherRepository_ThrowsNotFound()
    {
        var foreign = await AddRoleAsync(repositoryId: Guid.NewGuid());

        var act = () => DeleteHandler(RoleManager()).Handle(
            new DeleteRoleCommand(_repositoryId, foreign.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── DeleteRole: guards ──────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ADefaultRoleIsRefused()
    {
        var role = await AddRoleAsync("Developer", isDefault: true);

        var result = await DeleteHandler(RoleManager()).Handle(
            new DeleteRoleCommand(_repositoryId, role.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Default roles cannot be deleted");
        (await _db.Set<Role>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Delete_WhenMembersStillHoldTheRole_FailsAndNamesTheCount()
    {
        var role  = await AddRoleAsync("QA Lead");
        var alice = await AddUserAsync("Alice");
        var bob   = await AddUserAsync("Bob");
        await AddMemberAsync(role.Id, alice.Id);
        await AddMemberAsync(role.Id, bob.Id);

        var result = await DeleteHandler(RoleManager()).Handle(
            new DeleteRoleCommand(_repositoryId, role.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("2 member(s)");
        result.Error.Should().Contain("QA Lead");
        (await _db.Set<Role>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Delete_AfterMembersAreReassigned_Succeeds()
    {
        var role = await AddRoleAsync();

        var result = await DeleteHandler(RoleManager()).Handle(
            new DeleteRoleCommand(_repositoryId, role.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _db.Set<Role>().AnyAsync(r => r.Id == role.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_CountsHoldersAcrossEveryRepository()
    {
        // The assignment count is not scoped to this repository, so a membership elsewhere
        // still blocks the delete.
        var role  = await AddRoleAsync();
        var alice = await AddUserAsync("Alice");
        await AddMemberAsync(role.Id, alice.Id, repositoryId: Guid.NewGuid());

        var result = await DeleteHandler(RoleManager()).Handle(
            new DeleteRoleCommand(_repositoryId, role.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    // ── ListRoles ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ListRoles_WhenTheUserIsNotAMember_Throws()
    {
        var handler = RolesHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(new ListRolesQuery(_repositoryId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ListRoles_ReturnsOnlyRolesOfThisRepository()
    {
        await AddRoleAsync("Mine");
        await AddRoleAsync("Foreign", repositoryId: Guid.NewGuid());

        var result = await RolesHandler(MemberUser()).Handle(
            new ListRolesQuery(_repositoryId), CancellationToken.None);

        result.Should().ContainSingle().Which.Name.Should().Be("Mine");
    }

    [Fact]
    public async Task ListRoles_PutsDefaultRolesFirstThenSortsByName()
    {
        await AddRoleAsync("Zulu custom");
        await AddRoleAsync("Alpha custom");
        await AddRoleAsync("Zulu default",  isDefault: true);
        await AddRoleAsync("Alpha default", isDefault: true);

        var result = await RolesHandler(MemberUser()).Handle(
            new ListRolesQuery(_repositoryId), CancellationToken.None);

        result.Select(r => r.Name).Should().ContainInOrder(
            "Alpha default", "Zulu default", "Alpha custom", "Zulu custom");
    }

    [Fact]
    public async Task ListRoles_CountsTheMembersHoldingEachRole()
    {
        var busy  = await AddRoleAsync("Busy");
        var empty = await AddRoleAsync("Empty");
        var alice = await AddUserAsync("Alice");
        var bob   = await AddUserAsync("Bob");
        await AddMemberAsync(busy.Id, alice.Id);
        await AddMemberAsync(busy.Id, bob.Id);

        var result = await RolesHandler(MemberUser()).Handle(
            new ListRolesQuery(_repositoryId), CancellationToken.None);

        result.Single(r => r.Name == "Busy").MemberCount.Should().Be(2);
        result.Single(r => r.Name == "Empty").MemberCount.Should().Be(0);
    }

    [Fact]
    public async Task ListRoles_TheMemberCountIgnoresOtherRepositories()
    {
        var role  = await AddRoleAsync();
        var alice = await AddUserAsync("Alice");
        await AddMemberAsync(role.Id, alice.Id, repositoryId: Guid.NewGuid());

        var result = await RolesHandler(MemberUser()).Handle(
            new ListRolesQuery(_repositoryId), CancellationToken.None);

        result.Single().MemberCount.Should().Be(0,
            "the badge shows holders inside this repository");
    }

    [Fact]
    public async Task ListRoles_ProjectsThePermissionSet()
    {
        await AddRoleAsync("Custom", false, null,
            SystemFunction.ViewRepository, SystemFunction.ManageMembers);

        var result = await RolesHandler(MemberUser()).Handle(
            new ListRolesQuery(_repositoryId), CancellationToken.None);

        var role = result.Single();
        role.Description.Should().Be("Custom description");
        role.Permissions.Should().BeEquivalentTo(
            [SystemFunction.ViewRepository, SystemFunction.ManageMembers]);
    }

    // ── ListMembers ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ListMembers_WhenTheUserIsNotAMember_Throws()
    {
        var handler = MembersHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(new ListMembersQuery(_repositoryId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ListMembers_ReturnsOnlyMembersOfThisRepository()
    {
        var role  = await AddRoleAsync();
        var alice = await AddUserAsync("Alice");
        var bob   = await AddUserAsync("Bob");
        await AddMemberAsync(role.Id, alice.Id);
        await AddMemberAsync(role.Id, bob.Id, repositoryId: Guid.NewGuid());

        var result = await MembersHandler(MemberUser()).Handle(
            new ListMembersQuery(_repositoryId), CancellationToken.None);

        result.Should().ContainSingle().Which.UserName.Should().Be("Alice");
    }

    [Fact]
    public async Task ListMembers_OrdersByUserName()
    {
        var role  = await AddRoleAsync();
        var zach  = await AddUserAsync("Zach");
        var alice = await AddUserAsync("Alice");
        await AddMemberAsync(role.Id, zach.Id);
        await AddMemberAsync(role.Id, alice.Id);

        var result = await MembersHandler(MemberUser()).Handle(
            new ListMembersQuery(_repositoryId), CancellationToken.None);

        result.Select(m => m.UserName).Should().ContainInOrder("Alice", "Zach");
    }

    [Fact]
    public async Task ListMembers_WithARoleFilter_ReturnsOnlyThatDiscipline()
    {
        var role   = await AddRoleAsync();
        var alice  = await AddUserAsync("Alice");
        var bob    = await AddUserAsync("Bob");
        await AddMemberAsync(role.Id, alice.Id, defaultRole: "Tester");
        await AddMemberAsync(role.Id, bob.Id,   defaultRole: "Developer");

        var result = await MembersHandler(MemberUser()).Handle(
            new ListMembersQuery(_repositoryId, RoleFilter: "Tester"), CancellationToken.None);

        result.Should().ContainSingle().Which.UserName.Should().Be("Alice");
    }

    [Fact]
    public async Task ListMembers_ProjectsTheUserProfileAndRoleName()
    {
        var role  = await AddRoleAsync("QA Lead");
        var alice = await AddUserAsync("Alice");
        var member = await AddMemberAsync(role.Id, alice.Id, defaultRole: "Tester");

        var result = await MembersHandler(MemberUser()).Handle(
            new ListMembersQuery(_repositoryId), CancellationToken.None);

        var dto = result.Single();
        dto.MemberId.Should().Be(member.Id);
        dto.UserId.Should().Be(alice.Id);
        dto.UserEmail.Should().Be("alice@test.local");
        dto.AvatarClass.Should().Be("bg-sky-600");
        dto.DefaultRole.Should().Be("Tester");
        dto.RoleId.Should().Be(role.Id);
        dto.RoleName.Should().Be("QA Lead");
    }

    [Fact]
    public async Task ListMembers_WithNoMembers_ReturnsEmpty()
    {
        var result = await MembersHandler(MemberUser()).Handle(
            new ListMembersQuery(_repositoryId), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
