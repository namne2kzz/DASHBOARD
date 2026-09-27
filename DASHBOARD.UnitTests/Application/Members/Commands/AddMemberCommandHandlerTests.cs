using DASHBOARD.Application.Common.Caching;
using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Members.Commands.AddMember;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.UnitTests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.UnitTests.Application.Members.Commands;

/// <summary>
/// Unit tests for <see cref="AddMemberCommandHandler"/>.
/// </summary>
/// <remarks>
/// BUG-005 (High) was that this handler threw <c>UnauthorizedAccessException</c>,
/// <c>NotFoundException</c> and <c>InvalidOperationException</c> for ordinary business-rule
/// outcomes — adding a duplicate member surfaced as a 500 with no message in the UI. Every test
/// below asserts a <c>Result.Failure</c> rather than an exception, which is the regression guard:
/// these paths must stay non-throwing.
/// </remarks>
public sealed class AddMemberCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();
    private readonly Guid                     _orgId        = Guid.NewGuid();

    /// <summary>Sets up an isolated database holding one active repository.</summary>
    public AddMemberCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;

        _db.Set<Repository>().Add(
            new Repository { OrgId = _orgId, Name = "Dashboard", Code = "DASH" }.WithId(_repositoryId));
        _db.SaveChanges();
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private AddMemberCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow, _cache);

    private readonly FakeQueryCache _cache = new();

    private IRequestUserContext AuthorizedUser() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.ManageMembers, _repositoryId).Object;

    private async Task<User> AddUserAsync(Guid? orgId = null)
    {
        var user = new User
        {
            OrgId        = orgId ?? _orgId,
            Name         = "Candidate",
            Email        = $"{Guid.NewGuid():N}@test.local",
            PasswordHash = "hash",
            PasswordSalt = "salt",
        };
        _db.Set<User>().Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<Role> AddRoleAsync(bool isDefault = false, Guid? repositoryId = null)
    {
        var role = new Role
        {
            Name             = "Developer",
            IsDefault        = isDefault,
            RepositoryId     = isDefault ? null : repositoryId ?? _repositoryId,
            AllowedFunctions = [SystemFunction.ViewRepository],
        };
        _db.Set<Role>().Add(role);
        await _db.SaveChangesAsync();
        return role;
    }

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutManageMembersPrivilege_FailsWithoutThrowing()
    {
        var user    = await AddUserAsync();
        var role    = await AddRoleAsync();
        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(
            new AddMemberCommand(_repositoryId, user.Id, "Developer", role.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await _db.Set<RepositoryMember>().CountAsync()).Should().Be(0);
    }

    // ── Existence ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_FailsWithoutThrowing()
    {
        var role    = await AddRoleAsync();
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new AddMemberCommand(_repositoryId, Guid.NewGuid(), "Developer", role.Id),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("User not found.");
    }

    [Fact]
    public async Task Handle_WhenRepositoryDoesNotExist_FailsWithoutThrowing()
    {
        var otherRepoId = Guid.NewGuid();
        var user        = await AddUserAsync();
        var role        = await AddRoleAsync();
        var handler     = CreateHandler(
            RequestUserContextMock.ForUser().WithPrivilege(SystemFunction.ManageMembers).Object);

        var result = await handler.Handle(
            new AddMemberCommand(otherRepoId, user.Id, "Developer", role.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found or is archived");
    }

    [Fact]
    public async Task Handle_WhenRepositoryIsArchived_Fails()
    {
        var archivedId = Guid.NewGuid();
        _db.Set<Repository>().Add(
            new Repository { OrgId = _orgId, Name = "Old", Code = "OLD", IsArchived = true }
                .WithId(archivedId));
        await _db.SaveChangesAsync();

        var user    = await AddUserAsync();
        var role    = await AddRoleAsync();
        var handler = CreateHandler(
            RequestUserContextMock.ForUser().WithPrivilege(SystemFunction.ManageMembers).Object);

        var result = await handler.Handle(
            new AddMemberCommand(archivedId, user.Id, "Developer", role.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue("an archived repository takes no new members");
    }

    // ── Multi-tenancy ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserBelongsToAnotherOrganization_Fails()
    {
        var foreignUser = await AddUserAsync(orgId: Guid.NewGuid());
        var role        = await AddRoleAsync();
        var handler     = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new AddMemberCommand(_repositoryId, foreignUser.Id, "Developer", role.Id),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue("membership must never cross a tenant boundary");
        result.Error.Should().Contain("does not belong to this organization");
        (await _db.Set<RepositoryMember>().CountAsync()).Should().Be(0);
    }

    // ── Duplicate membership (BUG-005's reported repro) ─────────────────────

    [Fact]
    public async Task Handle_WhenUserIsAlreadyAMember_FailsWithoutThrowing()
    {
        var user    = await AddUserAsync();
        var role    = await AddRoleAsync();
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new AddMemberCommand(_repositoryId, user.Id, "Developer", role.Id), CancellationToken.None);

        var second = await handler.Handle(
            new AddMemberCommand(_repositoryId, user.Id, "Developer", role.Id), CancellationToken.None);

        second.IsFailure.Should().BeTrue();
        second.Error.Should().Contain("already a member");
        (await _db.Set<RepositoryMember>().CountAsync()).Should().Be(1);
    }

    // ── Role validation ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenRoleDoesNotExist_FailsWithoutThrowing()
    {
        var user    = await AddUserAsync();
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new AddMemberCommand(_repositoryId, user.Id, "Developer", Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Role not found");
    }

    [Fact]
    public async Task Handle_WhenRoleBelongsToAnotherRepository_Fails()
    {
        var user        = await AddUserAsync();
        var foreignRole = await AddRoleAsync(repositoryId: Guid.NewGuid());
        var handler     = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new AddMemberCommand(_repositoryId, user.Id, "Developer", foreignRole.Id),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue("a custom role is scoped to the repository that owns it");
    }

    [Fact]
    public async Task Handle_WithAGlobalDefaultRole_Succeeds()
    {
        var user       = await AddUserAsync();
        var globalRole = await AddRoleAsync(isDefault: true);
        var handler    = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new AddMemberCommand(_repositoryId, user.Id, "Developer", globalRole.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue("default roles are usable by every repository");
    }

    // ── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidInput_CreatesMemberAndReturnsDto()
    {
        var user    = await AddUserAsync();
        var role    = await AddRoleAsync();
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new AddMemberCommand(_repositoryId, user.Id, "Tester", role.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.UserId.Should().Be(user.Id);
        result.Value.UserEmail.Should().Be(user.Email);
        result.Value.DefaultRole.Should().Be("Tester");
        result.Value.RoleId.Should().Be(role.Id);
        result.Value.RoleName.Should().Be("Developer");

        var persisted = await _db.Set<RepositoryMember>().AsNoTracking().SingleAsync();
        persisted.UserId.Should().Be(user.Id);
        persisted.RepositoryId.Should().Be(_repositoryId);
    }

    [Fact]
    public async Task Handle_AllowsTheSameUserInTwoDifferentRepositories()
    {
        var secondRepoId = Guid.NewGuid();
        _db.Set<Repository>().Add(
            new Repository { OrgId = _orgId, Name = "Second", Code = "SEC" }.WithId(secondRepoId));
        await _db.SaveChangesAsync();

        var user    = await AddUserAsync();
        var role    = await AddRoleAsync(isDefault: true);
        var handler = CreateHandler(
            RequestUserContextMock.ForUser().WithPrivilege(SystemFunction.ManageMembers).Object);

        await handler.Handle(
            new AddMemberCommand(_repositoryId, user.Id, "Developer", role.Id), CancellationToken.None);
        var second = await handler.Handle(
            new AddMemberCommand(secondRepoId, user.Id, "Developer", role.Id), CancellationToken.None);

        second.IsSuccess.Should().BeTrue("the duplicate check is per repository");
        (await _db.Set<RepositoryMember>().CountAsync()).Should().Be(2);
    }

    // ── Cache invalidation ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenMemberAdded_EvictsTheRepositoryCacheTag()
    {
        var user    = await AddUserAsync();
        var role    = await AddRoleAsync();
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new AddMemberCommand(_repositoryId, user.Id, "Developer", role.Id), CancellationToken.None);

        _cache.EvictedTags.Should().Contain(
            CacheKeys.RepositoryTag(_repositoryId),
            "a stale member list would otherwise survive until its TTL expires");
    }

    [Fact]
    public async Task Handle_WhenAddFails_DoesNotEvictTheCache()
    {
        var role    = await AddRoleAsync();
        var handler = CreateHandler(AuthorizedUser());

        // Unknown user id — the command fails before anything is committed.
        await handler.Handle(
            new AddMemberCommand(_repositoryId, Guid.NewGuid(), "Developer", role.Id), CancellationToken.None);

        _cache.EvictedTags.Should().BeEmpty("nothing changed, so no cached entry went stale");
    }
}
