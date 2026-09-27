using DASHBOARD.Application.Common.Guards;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.UnitTests.Common;

namespace DASHBOARD.UnitTests.Application.Common.Guards;

/// <summary>
/// Unit tests for <see cref="ManageSettingsGuard"/>.
/// </summary>
/// <remarks>
/// This guard exists because of BUG-004 (High): the original checks were hard-coded to the
/// <c>"Scrum Master"</c> role name, so a repository whose only <see cref="SystemFunction.ManageMembers"/>
/// holder carried a different role could be left with nobody able to manage it. Per roles.dod.md §3 the
/// rule must be evaluated on actual permissions, never on a role or discipline name — the
/// "custom role name" tests below are the regression guard for that.
/// </remarks>
public sealed class ManageSettingsGuardTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public ManageSettingsGuardTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private async Task<Role> AddRoleAsync(string name, params SystemFunction[] functions)
    {
        var role = new Role
        {
            RepositoryId     = _repositoryId,
            Name             = name,
            AllowedFunctions = [.. functions],
        };
        _db.Set<Role>().Add(role);
        await _db.SaveChangesAsync();
        return role;
    }

    private async Task<RepositoryMember> AddMemberAsync(Guid roleId, Guid? repositoryId = null)
    {
        var member = new RepositoryMember
        {
            RepositoryId = repositoryId ?? _repositoryId,
            UserId       = Guid.NewGuid(),
            RoleId       = roleId,
            DefaultRole  = "Developer",
        };
        _db.Set<RepositoryMember>().Add(member);
        await _db.SaveChangesAsync();
        return member;
    }

    private Task<bool> WouldOrphanAsync(Guid excludedMemberId, Guid? overrideRoleId = null) =>
        ManageSettingsGuard.WouldOrphanManageSettingsAsync(
            _db, _repositoryId, excludedMemberId, overrideRoleId, CancellationToken.None);

    // ── BUG-004 regression: the rule follows permissions, not role names ─────

    [Fact]
    public async Task WouldOrphan_WhenTheOnlyManagerHoldsACustomNamedRole_ReturnsTrue()
    {
        // The role is called neither "Scrum Master" nor anything else special — only its
        // permission set matters.
        var managerRole = await AddRoleAsync("Chief Widget Officer", SystemFunction.ManageMembers);
        var sole        = await AddMemberAsync(managerRole.Id);

        (await WouldOrphanAsync(sole.Id)).Should().BeTrue(
            "the guard must count actual ManageMembers holders regardless of what the role is called");
    }

    [Fact]
    public async Task WouldOrphan_WhenAnotherCustomNamedRoleStillGrantsIt_ReturnsFalse()
    {
        var roleA = await AddRoleAsync("Project Manager", SystemFunction.ManageMembers);
        var roleB = await AddRoleAsync("Delivery Lead",   SystemFunction.ManageMembers);
        var leaving = await AddMemberAsync(roleA.Id);
        await AddMemberAsync(roleB.Id);

        (await WouldOrphanAsync(leaving.Id)).Should().BeFalse(
            "a differently-named role that grants the permission still counts as cover");
    }

    // ── Headcount ───────────────────────────────────────────────────────────

    [Fact]
    public async Task WouldOrphan_WhenAnotherMemberKeepsThePermission_ReturnsFalse()
    {
        var managerRole = await AddRoleAsync("Manager", SystemFunction.ManageMembers);
        var leaving     = await AddMemberAsync(managerRole.Id);
        await AddMemberAsync(managerRole.Id);

        (await WouldOrphanAsync(leaving.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task WouldOrphan_WhenRemainingMembersLackThePermission_ReturnsTrue()
    {
        var managerRole = await AddRoleAsync("Manager",   SystemFunction.ManageMembers);
        var plainRole   = await AddRoleAsync("Developer", SystemFunction.ViewRepository, SystemFunction.EditWorkItem);
        var leaving     = await AddMemberAsync(managerRole.Id);
        await AddMemberAsync(plainRole.Id);

        (await WouldOrphanAsync(leaving.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task WouldOrphan_WhenTheRepositoryHasNoOtherMembers_ReturnsTrue()
    {
        var managerRole = await AddRoleAsync("Manager", SystemFunction.ManageMembers);
        var sole        = await AddMemberAsync(managerRole.Id);

        (await WouldOrphanAsync(sole.Id)).Should().BeTrue();
    }

    // ── Repository scoping ──────────────────────────────────────────────────

    [Fact]
    public async Task WouldOrphan_IgnoresManagersOfOtherRepositories()
    {
        var managerRole = await AddRoleAsync("Manager", SystemFunction.ManageMembers);
        var leaving     = await AddMemberAsync(managerRole.Id);
        await AddMemberAsync(managerRole.Id, repositoryId: Guid.NewGuid());

        (await WouldOrphanAsync(leaving.Id)).Should().BeTrue(
            "cover from a different repository does not help this one");
    }

    // ── Reassignment (overrideRoleId) ───────────────────────────────────────

    [Fact]
    public async Task WouldOrphan_WhenReassignedToARoleThatKeepsThePermission_ReturnsFalse()
    {
        var managerRole = await AddRoleAsync("Manager",     SystemFunction.ManageMembers);
        var otherAdmin  = await AddRoleAsync("Team Leader", SystemFunction.ManageMembers);
        var changing    = await AddMemberAsync(managerRole.Id);

        (await WouldOrphanAsync(changing.Id, overrideRoleId: otherAdmin.Id)).Should().BeFalse(
            "the member keeps the permission under their new role");
    }

    [Fact]
    public async Task WouldOrphan_WhenReassignedToARoleThatDropsThePermission_ReturnsTrue()
    {
        var managerRole = await AddRoleAsync("Manager",   SystemFunction.ManageMembers);
        var plainRole   = await AddRoleAsync("Developer", SystemFunction.ViewRepository);
        var changing    = await AddMemberAsync(managerRole.Id);

        (await WouldOrphanAsync(changing.Id, overrideRoleId: plainRole.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task WouldOrphan_WhenOverrideRoleDoesNotExist_ReturnsTrue()
    {
        var managerRole = await AddRoleAsync("Manager", SystemFunction.ManageMembers);
        var changing    = await AddMemberAsync(managerRole.Id);

        (await WouldOrphanAsync(changing.Id, overrideRoleId: Guid.NewGuid())).Should().BeTrue(
            "a missing role cannot be assumed to grant anything");
    }

    [Fact]
    public async Task WouldOrphan_WhenOthersAlreadyCover_IgnoresTheOverrideRole()
    {
        var managerRole = await AddRoleAsync("Manager",   SystemFunction.ManageMembers);
        var plainRole   = await AddRoleAsync("Developer", SystemFunction.ViewRepository);
        var changing    = await AddMemberAsync(managerRole.Id);
        await AddMemberAsync(managerRole.Id);

        (await WouldOrphanAsync(changing.Id, overrideRoleId: plainRole.Id)).Should().BeFalse(
            "someone else still holds the permission, so the demotion is safe");
    }

    // ── Permission specificity ──────────────────────────────────────────────

    [Fact]
    public async Task WouldOrphan_DoesNotAcceptOtherManagePermissionsAsCover()
    {
        var managerRole = await AddRoleAsync("Manager", SystemFunction.ManageMembers);
        var roleAdmin   = await AddRoleAsync("Role Admin", SystemFunction.ManageRoles, SystemFunction.ManageMetadata);
        var leaving     = await AddMemberAsync(managerRole.Id);
        await AddMemberAsync(roleAdmin.Id);

        (await WouldOrphanAsync(leaving.Id)).Should().BeTrue(
            "ManageRoles and ManageMetadata are separate permissions from ManageMembers");
    }

    [Fact]
    public async Task WouldOrphan_AcceptsARoleThatGrantsManageMembersAmongOthers()
    {
        var managerRole = await AddRoleAsync("Manager", SystemFunction.ManageMembers);
        var broadRole   = await AddRoleAsync("Owner",
            SystemFunction.ViewRepository, SystemFunction.ManageMembers, SystemFunction.ManageRoles);
        var leaving     = await AddMemberAsync(managerRole.Id);
        await AddMemberAsync(broadRole.Id);

        (await WouldOrphanAsync(leaving.Id)).Should().BeFalse();
    }
}
