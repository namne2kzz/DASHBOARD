using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Roles.Commands.UpdateRole;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Tests.Application.Roles.Commands;

/// <summary>
/// Unit tests for <see cref="UpdateRoleCommandHandler"/>.
/// </summary>
/// <remarks>
/// Part (2) of BUG-004 (High) was that this handler had <em>no</em> ManageMembers guard at all:
/// an admin could strip <see cref="SystemFunction.ManageMembers"/> from the only role granting it,
/// leaving the repository with nobody able to reach Settings and no way to recover. The
/// "only source" tests below are the regression guard for that.
/// </remarks>
public sealed class UpdateRoleCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public UpdateRoleCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private UpdateRoleCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private IRequestUserContext AuthorizedUser() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.ManageRoles, _repositoryId).Object;

    private async Task<Role> AddRoleAsync(
        string name, bool isDefault = false, Guid? repositoryId = null, params SystemFunction[] functions)
    {
        var role = new Role
        {
            Name             = name,
            Description      = "Original description",
            IsDefault        = isDefault,
            RepositoryId     = isDefault ? null : repositoryId ?? _repositoryId,
            AllowedFunctions = [.. functions],
        };
        _db.Set<Role>().Add(role);
        await _db.SaveChangesAsync();
        return role;
    }

    private async Task AddMemberAsync(Guid roleId, Guid? repositoryId = null)
    {
        _db.Set<RepositoryMember>().Add(new RepositoryMember
        {
            RepositoryId = repositoryId ?? _repositoryId,
            UserId       = Guid.NewGuid(),
            RoleId       = roleId,
            DefaultRole  = "Developer",
        });
        await _db.SaveChangesAsync();
    }

    private UpdateRoleCommand Command(
        Guid roleId, string name = "Updated Name", string description = "Updated description",
        params SystemFunction[] functions) =>
        new(_repositoryId, roleId, name, description, [.. functions]);

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutManageRolesPrivilege_FailsAndLeavesRoleUnchanged()
    {
        var role    = await AddRoleAsync("Original", functions: SystemFunction.ViewRepository);
        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var result = await handler.Handle(
            Command(role.Id, functions: SystemFunction.ViewRepository), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("permission");

        var unchanged = await _db.Set<Role>().AsNoTracking().SingleAsync(r => r.Id == role.Id);
        unchanged.Name.Should().Be("Original");
    }

    // ── Not found and default roles ─────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenRoleDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            Command(Guid.NewGuid(), functions: SystemFunction.ViewRepository), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenRoleBelongsToAnotherRepository_ThrowsNotFound()
    {
        var foreign = await AddRoleAsync("Foreign", repositoryId: Guid.NewGuid(),
            functions: SystemFunction.ViewRepository);
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            Command(foreign.Id, functions: SystemFunction.ViewRepository), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenRoleIsADefaultRole_Fails()
    {
        // A default role has a null RepositoryId, so it is not reachable through this
        // repository-scoped lookup at all.
        var defaultRole = await AddRoleAsync("Developer", isDefault: true,
            functions: SystemFunction.ViewRepository);
        var handler     = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            Command(defaultRole.Id, functions: SystemFunction.ViewRepository), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>(
            "default roles are global and cannot be edited through a repository");
    }

    // ── Name uniqueness ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenRenamingOntoAnotherCustomRole_Fails()
    {
        var target = await AddRoleAsync("Original", functions: SystemFunction.ViewRepository);
        await AddRoleAsync("Taken", functions: SystemFunction.ViewRepository);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(target.Id, name: "Taken", functions: SystemFunction.ViewRepository),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exists");
    }

    [Fact]
    public async Task Handle_KeepingItsOwnNameIsNotANameConflict()
    {
        var role    = await AddRoleAsync("Stable", functions: SystemFunction.ViewRepository);
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(role.Id, name: "Stable", functions: SystemFunction.ViewRepository),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenTheSameNameExistsInAnotherRepository_Succeeds()
    {
        var role = await AddRoleAsync("Original", functions: SystemFunction.ViewRepository);
        await AddRoleAsync("Taken", repositoryId: Guid.NewGuid(), functions: SystemFunction.ViewRepository);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(role.Id, name: "Taken", functions: SystemFunction.ViewRepository),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue("role names are unique per repository");
    }

    // ── BUG-004 (2): ManageMembers orphan guard ─────────────────────────────

    [Fact]
    public async Task Handle_WhenStrippingManageMembersFromItsOnlySource_Fails()
    {
        var soleSource = await AddRoleAsync("Custom Admin", functions: SystemFunction.ManageMembers);
        await AddMemberAsync(soleSource.Id);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(soleSource.Id, functions: SystemFunction.ViewRepository), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("only source of that permission");

        var unchanged = await _db.Set<Role>().AsNoTracking().SingleAsync(r => r.Id == soleSource.Id);
        unchanged.AllowedFunctions.Should().Contain(SystemFunction.ManageMembers);
    }

    [Fact]
    public async Task Handle_WhenAnotherRoleStillGrantsManageMembers_AllowsStripping()
    {
        var roleA = await AddRoleAsync("Admin A", functions: SystemFunction.ManageMembers);
        var roleB = await AddRoleAsync("Admin B", functions: SystemFunction.ManageMembers);
        await AddMemberAsync(roleA.Id);
        await AddMemberAsync(roleB.Id);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(roleA.Id, functions: SystemFunction.ViewRepository), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("another member still covers ManageMembers");
    }

    [Fact]
    public async Task Handle_WhenNobodyHoldsTheRole_AllowsStripping()
    {
        var unusedRole = await AddRoleAsync("Unused Admin", functions: SystemFunction.ManageMembers);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(unusedRole.Id, functions: SystemFunction.ViewRepository), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(
            "a role nobody is assigned cannot be anybody's only source of the permission");
    }

    [Fact]
    public async Task Handle_WhenTheRoleKeepsManageMembers_SkipsTheGuard()
    {
        var soleSource = await AddRoleAsync("Custom Admin", functions: SystemFunction.ManageMembers);
        await AddMemberAsync(soleSource.Id);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(soleSource.Id, "Updated Name", "Updated description",
                SystemFunction.ManageMembers, SystemFunction.ViewRepository),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue("the permission is not being removed, so nothing is orphaned");
    }

    [Fact]
    public async Task Handle_DoesNotCountCoverFromAnotherRepository()
    {
        var soleSource  = await AddRoleAsync("Custom Admin", functions: SystemFunction.ManageMembers);
        var foreignRole = await AddRoleAsync("Foreign Admin", repositoryId: Guid.NewGuid(),
            functions: SystemFunction.ManageMembers);
        await AddMemberAsync(soleSource.Id);
        await AddMemberAsync(foreignRole.Id, repositoryId: Guid.NewGuid());

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(soleSource.Id, functions: SystemFunction.ViewRepository), CancellationToken.None);

        result.IsFailure.Should().BeTrue("cover must come from within the same repository");
    }

    // ── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_UpdatesNameDescriptionFunctionsAndTimestamp()
    {
        var role    = await AddRoleAsync("Original", functions: SystemFunction.ViewRepository);
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(role.Id, "Renamed Role", "New description",
                SystemFunction.ViewRepository, SystemFunction.EditWorkItem),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = await _db.Set<Role>().AsNoTracking().SingleAsync(r => r.Id == role.Id);
        updated.Name.Should().Be("Renamed Role");
        updated.Description.Should().Be("New description");
        updated.AllowedFunctions.Should()
               .BeEquivalentTo([SystemFunction.ViewRepository, SystemFunction.EditWorkItem]);
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ReplacesTheFunctionSetRatherThanMergingIt()
    {
        var role = await AddRoleAsync("Original", false, null,
            SystemFunction.ViewRepository, SystemFunction.EditWorkItem, SystemFunction.DeleteWorkItem);

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            Command(role.Id, functions: SystemFunction.ViewRepository), CancellationToken.None);

        var updated = await _db.Set<Role>().AsNoTracking().SingleAsync(r => r.Id == role.Id);
        updated.AllowedFunctions.Should().ContainSingle()
               .Which.Should().Be(SystemFunction.ViewRepository);
    }
}
