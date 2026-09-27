using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Roles.Commands.CloneRole;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.UnitTests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.UnitTests.Application.Roles.Commands;

/// <summary>Unit tests for <see cref="CloneRoleCommandHandler"/>.</summary>
/// <remarks>
/// Like <c>CreateRole</c>, this handler was part of BUG-005 (High): business-rule outcomes threw
/// instead of returning a result. Every failure test below asserts a <c>Result.Failure</c>.
/// </remarks>
public sealed class CloneRoleCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public CloneRoleCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private CloneRoleCommandHandler CreateHandler(IRequestUserContext user) =>
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
            Description      = "Source description",
            IsDefault        = isDefault,
            RepositoryId     = isDefault ? null : repositoryId ?? _repositoryId,
            AllowedFunctions = functions.Length > 0 ? [.. functions] : [SystemFunction.ViewRepository],
        };
        _db.Set<Role>().Add(role);
        await _db.SaveChangesAsync();
        return role;
    }

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutManageRolesPrivilege_FailsWithoutThrowing()
    {
        var source  = await AddRoleAsync("Source");
        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(
            new CloneRoleCommand(_repositoryId, source.Id, "Copy"), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await _db.Set<Role>().CountAsync()).Should().Be(1);
    }

    // ── Source resolution ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSourceRoleDoesNotExist_FailsWithoutThrowing()
    {
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new CloneRoleCommand(_repositoryId, Guid.NewGuid(), "Copy"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Role not found");
    }

    [Fact]
    public async Task Handle_WhenSourceBelongsToAnotherRepository_Fails()
    {
        var foreign = await AddRoleAsync("Foreign", repositoryId: Guid.NewGuid());
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new CloneRoleCommand(_repositoryId, foreign.Id, "Copy"), CancellationToken.None);

        result.IsFailure.Should().BeTrue("a custom role is only visible to the repository that owns it");
    }

    [Fact]
    public async Task Handle_CanCloneAGlobalDefaultRole()
    {
        var defaultRole = await AddRoleAsync("Developer", true, null,
            SystemFunction.ViewRepository, SystemFunction.EditWorkItem);
        var handler     = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new CloneRoleCommand(_repositoryId, defaultRole.Id, "Developer Plus"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("default roles are the usual starting point for a clone");
        result.Value!.IsDefault.Should().BeFalse("the clone is always a custom role");
        result.Value.RepositoryId.Should().Be(_repositoryId);
    }

    // ── Name uniqueness ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenNewNameIsAlreadyTaken_FailsWithoutThrowing()
    {
        var source = await AddRoleAsync("Source");
        await AddRoleAsync("Taken");

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new CloneRoleCommand(_repositoryId, source.Id, "Taken"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exists");
        (await _db.Set<Role>().CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Handle_WhenTheNameIsTakenInAnotherRepository_Succeeds()
    {
        var source = await AddRoleAsync("Source");
        await AddRoleAsync("Taken", repositoryId: Guid.NewGuid());

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new CloneRoleCommand(_repositoryId, source.Id, "Taken"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // ── Cloning behaviour ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CopiesTheSourcePermissionSet()
    {
        var source = await AddRoleAsync("Source", false, null,
            SystemFunction.ViewRepository, SystemFunction.ManageMembers, SystemFunction.EditWorkItem);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new CloneRoleCommand(_repositoryId, source.Id, "Copy"), CancellationToken.None);

        result.Value!.Permissions.Should().BeEquivalentTo(source.AllowedFunctions);
    }

    [Fact]
    public async Task Handle_CopiesThePermissionsByValueNotByReference()
    {
        var source  = await AddRoleAsync("Source", false, null, SystemFunction.ViewRepository);
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new CloneRoleCommand(_repositoryId, source.Id, "Copy"), CancellationToken.None);

        // Mutating the clone's stored list must not reach through to the source. The handler copies
        // with [..source.AllowedFunctions]; sharing the list instance instead would corrupt the source.
        var clone = await _db.Set<Role>().AsTracking().SingleAsync(r => r.Id == result.Value!.Id);
        clone.AllowedFunctions.Add(SystemFunction.ManageMembers);
        await _db.SaveChangesAsync();

        var unchangedSource = await _db.Set<Role>().AsNoTracking().SingleAsync(r => r.Id == source.Id);
        unchangedSource.AllowedFunctions.Should().ContainSingle()
            .Which.Should().Be(SystemFunction.ViewRepository,
                "editing the clone must never mutate the source role");
    }

    [Fact]
    public async Task Handle_DescribesTheCloneAsDerivedFromItsSource()
    {
        var source  = await AddRoleAsync("Scrum Master");
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new CloneRoleCommand(_repositoryId, source.Id, "Scrum Master Plus"), CancellationToken.None);

        result.Value!.Description.Should().Be("Clone of 'Scrum Master'");
    }

    [Fact]
    public async Task Handle_PersistsTheCloneAlongsideTheSource()
    {
        var source  = await AddRoleAsync("Source");
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new CloneRoleCommand(_repositoryId, source.Id, "Copy"), CancellationToken.None);

        (await _db.Set<Role>().CountAsync()).Should().Be(2);

        var clone = await _db.Set<Role>().AsNoTracking().SingleAsync(r => r.Id == result.Value!.Id);
        clone.Name.Should().Be("Copy");
        clone.RepositoryId.Should().Be(_repositoryId);
        clone.IsDefault.Should().BeFalse();
    }
}
