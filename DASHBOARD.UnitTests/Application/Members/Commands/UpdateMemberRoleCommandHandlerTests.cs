using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Members.Commands.UpdateMemberRole;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.UnitTests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.UnitTests.Application.Members.Commands;

/// <summary>Unit tests for <see cref="UpdateMemberRoleCommandHandler"/>.</summary>
public sealed class UpdateMemberRoleCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public UpdateMemberRoleCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private UpdateMemberRoleCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow, _cache, _publisher);

    private readonly FakeQueryCache _cache = new();
    private readonly RecordingPublishEndpoint _publisher = new();

    private IRequestUserContext AuthorizedUser() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.ManageMembers, _repositoryId).Object;

    private async Task<Role> AddRoleAsync(
        string name, bool isDefault = false, Guid? repositoryId = null, params SystemFunction[] functions)
    {
        var role = new Role
        {
            Name             = name,
            IsDefault        = isDefault,
            RepositoryId     = isDefault ? null : repositoryId ?? _repositoryId,
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

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutManageMembersPrivilege_FailsAndLeavesRoleUnchanged()
    {
        var managerRole = await AddRoleAsync("Manager", functions: SystemFunction.ManageMembers);
        var plainRole   = await AddRoleAsync("Developer", functions: SystemFunction.ViewRepository);
        var member      = await AddMemberAsync(managerRole.Id);
        await AddMemberAsync(managerRole.Id);

        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(
            new UpdateMemberRoleCommand(_repositoryId, member.Id, "Tester", plainRole.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();

        var unchanged = await _db.Set<RepositoryMember>().AsNoTracking().SingleAsync(m => m.Id == member.Id);
        unchanged.RoleId.Should().Be(managerRole.Id);
    }

    // ── Not found ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenMemberDoesNotExist_ThrowsNotFound()
    {
        var role    = await AddRoleAsync("Developer", functions: SystemFunction.ViewRepository);
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new UpdateMemberRoleCommand(_repositoryId, Guid.NewGuid(), "Tester", role.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenMemberBelongsToAnotherRepository_ThrowsNotFound()
    {
        var role    = await AddRoleAsync("Developer", functions: SystemFunction.ViewRepository);
        var foreign = await AddMemberAsync(role.Id, repositoryId: Guid.NewGuid());
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new UpdateMemberRoleCommand(_repositoryId, foreign.Id, "Tester", role.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Role validation ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenNewRoleDoesNotExist_Fails()
    {
        var managerRole = await AddRoleAsync("Manager", functions: SystemFunction.ManageMembers);
        var member      = await AddMemberAsync(managerRole.Id);
        await AddMemberAsync(managerRole.Id);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new UpdateMemberRoleCommand(_repositoryId, member.Id, "Tester", Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Role not found");
    }

    [Fact]
    public async Task Handle_WhenNewRoleBelongsToAnotherRepository_Fails()
    {
        var managerRole = await AddRoleAsync("Manager", functions: SystemFunction.ManageMembers);
        var foreignRole = await AddRoleAsync("Foreign", repositoryId: Guid.NewGuid(),
            functions: SystemFunction.ManageMembers);
        var member      = await AddMemberAsync(managerRole.Id);
        await AddMemberAsync(managerRole.Id);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new UpdateMemberRoleCommand(_repositoryId, member.Id, "Tester", foreignRole.Id),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue("a custom role is scoped to the repository that owns it");
    }

    [Fact]
    public async Task Handle_WithAGlobalDefaultRole_Succeeds()
    {
        var managerRole = await AddRoleAsync("Manager", functions: SystemFunction.ManageMembers);
        var globalRole  = await AddRoleAsync("Reader", isDefault: true,
            functions: SystemFunction.ViewRepository);
        var member      = await AddMemberAsync(managerRole.Id);
        await AddMemberAsync(managerRole.Id);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new UpdateMemberRoleCommand(_repositoryId, member.Id, "Tester", globalRole.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // ── ManageMembers guard ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenDemotingTheLastManageMembersHolder_Fails()
    {
        var managerRole = await AddRoleAsync("Manager",   functions: SystemFunction.ManageMembers);
        var plainRole   = await AddRoleAsync("Developer", functions: SystemFunction.ViewRepository);
        var soleManager = await AddMemberAsync(managerRole.Id);
        await AddMemberAsync(plainRole.Id);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new UpdateMemberRoleCommand(_repositoryId, soleManager.Id, "Developer", plainRole.Id),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("last member with ManageMembers");

        var unchanged = await _db.Set<RepositoryMember>().AsNoTracking()
            .SingleAsync(m => m.Id == soleManager.Id);
        unchanged.RoleId.Should().Be(managerRole.Id);
    }

    [Fact]
    public async Task Handle_WhenTheNewRoleAlsoGrantsManageMembers_Succeeds()
    {
        var managerRole = await AddRoleAsync("Manager",       functions: SystemFunction.ManageMembers);
        var otherAdmin  = await AddRoleAsync("Delivery Lead", functions: SystemFunction.ManageMembers);
        var soleManager = await AddMemberAsync(managerRole.Id);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new UpdateMemberRoleCommand(_repositoryId, soleManager.Id, "Tester", otherAdmin.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(
            "the sole manager keeps the permission under their new role");
    }

    [Fact]
    public async Task Handle_WhenAnotherManagerCovers_AllowsDemotion()
    {
        var managerRole = await AddRoleAsync("Manager",   functions: SystemFunction.ManageMembers);
        var plainRole   = await AddRoleAsync("Developer", functions: SystemFunction.ViewRepository);
        var demoted     = await AddMemberAsync(managerRole.Id);
        await AddMemberAsync(managerRole.Id);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new UpdateMemberRoleCommand(_repositoryId, demoted.Id, "Developer", plainRole.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // ── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_UpdatesDefaultRoleRoleIdAndTimestamp()
    {
        var managerRole = await AddRoleAsync("Manager", functions: SystemFunction.ManageMembers);
        var newRole     = await AddRoleAsync("Tester",  functions: SystemFunction.ViewRepository);
        var member      = await AddMemberAsync(managerRole.Id);
        await AddMemberAsync(managerRole.Id);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new UpdateMemberRoleCommand(_repositoryId, member.Id, "Tester", newRole.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = await _db.Set<RepositoryMember>().AsNoTracking().SingleAsync(m => m.Id == member.Id);
        updated.DefaultRole.Should().Be("Tester");
        updated.RoleId.Should().Be(newRole.Id);
        updated.UpdatedAt.Should().NotBeNull();
    }
}
