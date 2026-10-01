using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Members.Commands.RemoveMember;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.UnitTests.Common;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationEvents;

namespace DASHBOARD.UnitTests.Application.Members.Commands;

/// <summary>Unit tests for <see cref="RemoveMemberCommandHandler"/>.</summary>
public sealed class RemoveMemberCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public RemoveMemberCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private RemoveMemberCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow, _cache, _publisher);

    private readonly FakeQueryCache _cache = new();
    private readonly RecordingPublishEndpoint _publisher = new();

    private IRequestUserContext AuthorizedUser() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.ManageMembers, _repositoryId).Object;

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

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutManageMembersPrivilege_FailsAndKeepsMember()
    {
        var role    = await AddRoleAsync("Manager", SystemFunction.ManageMembers);
        var member  = await AddMemberAsync(role.Id);
        await AddMemberAsync(role.Id);

        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(
            new RemoveMemberCommand(_repositoryId, member.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await _db.Set<RepositoryMember>().CountAsync()).Should().Be(2);
    }

    // ── Not found ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenMemberDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new RemoveMemberCommand(_repositoryId, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenMemberBelongsToAnotherRepository_ThrowsNotFound()
    {
        var role    = await AddRoleAsync("Manager", SystemFunction.ManageMembers);
        var foreign = await AddMemberAsync(role.Id, repositoryId: Guid.NewGuid());
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new RemoveMemberCommand(_repositoryId, foreign.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── ManageMembers guard ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenRemovingTheLastManageMembersHolder_Fails()
    {
        var managerRole = await AddRoleAsync("Manager",   SystemFunction.ManageMembers);
        var plainRole   = await AddRoleAsync("Developer", SystemFunction.ViewRepository);
        var soleManager = await AddMemberAsync(managerRole.Id);
        await AddMemberAsync(plainRole.Id);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new RemoveMemberCommand(_repositoryId, soleManager.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("last member with ManageMembers");
        (await _db.Set<RepositoryMember>().AnyAsync(m => m.Id == soleManager.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenAnotherManageMembersHolderRemains_Succeeds()
    {
        var managerRole = await AddRoleAsync("Manager", SystemFunction.ManageMembers);
        var leaving     = await AddMemberAsync(managerRole.Id);
        await AddMemberAsync(managerRole.Id);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new RemoveMemberCommand(_repositoryId, leaving.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _db.Set<RepositoryMember>().AnyAsync(m => m.Id == leaving.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenCoverComesFromADifferentlyNamedRole_Succeeds()
    {
        // BUG-004: cover must be recognised by permission, not by the role being called
        // something specific like "Scrum Master".
        var managerRole = await AddRoleAsync("Scrum Master",    SystemFunction.ManageMembers);
        var otherRole   = await AddRoleAsync("Delivery Lead",   SystemFunction.ManageMembers);
        var leaving     = await AddMemberAsync(managerRole.Id);
        await AddMemberAsync(otherRole.Id);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new RemoveMemberCommand(_repositoryId, leaving.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_RemovingAPlainMemberIsAlwaysAllowed()
    {
        var managerRole = await AddRoleAsync("Manager",   SystemFunction.ManageMembers);
        var plainRole   = await AddRoleAsync("Developer", SystemFunction.ViewRepository);
        await AddMemberAsync(managerRole.Id);
        var plainMember = await AddMemberAsync(plainRole.Id);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new RemoveMemberCommand(_repositoryId, plainMember.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _db.Set<RepositoryMember>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_DoesNotCountManagersOfOtherRepositoriesAsCover()
    {
        var managerRole = await AddRoleAsync("Manager", SystemFunction.ManageMembers);
        var soleManager = await AddMemberAsync(managerRole.Id);
        await AddMemberAsync(managerRole.Id, repositoryId: Guid.NewGuid());

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new RemoveMemberCommand(_repositoryId, soleManager.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue("cover must come from within the same repository");
    }

    // ── Cross-system cache invalidation ─────────────────────────────────────

    [Fact]
    public async Task Handle_WhenMemberRemoved_PublishesTheDirectoryChangeForHub()
    {
        var managerRole = await AddRoleAsync("Manager", SystemFunction.ManageMembers);
        var plainRole   = await AddRoleAsync("Developer");
        await AddMemberAsync(managerRole.Id);                     // keeps the ManageMembers guard satisfied
        var plainMember = await AddMemberAsync(plainRole.Id);

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(new RemoveMemberCommand(_repositoryId, plainMember.Id), CancellationToken.None);

        // Without this event HUB keeps the revoked user in its cached memberships, and they pass its
        // membership check for up to three minutes after losing access.
        var published = _publisher.PublishedOf<MemberDirectoryChangedEvent>();
        published.Should().ContainSingle();
        published[0].RepositoryId.Should().Be(_repositoryId);
        published[0].UserId.Should().Be(plainMember.UserId);
    }

    [Fact]
    public async Task Handle_WhenRemovalRejected_PublishesNothing()
    {
        var managerRole = await AddRoleAsync("Manager", SystemFunction.ManageMembers);
        var soleManager = await AddMemberAsync(managerRole.Id);

        var handler = CreateHandler(AuthorizedUser());

        // Guard rejects this: removing the last member who can manage members.
        var result = await handler.Handle(
            new RemoveMemberCommand(_repositoryId, soleManager.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _publisher.Published.Should().BeEmpty("nothing changed, so HUB has nothing to invalidate");
    }
}
