using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Contracts;
using DASHBOARD.Application.Invitations.Commands.CreateInvitation;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Tests.Common;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DASHBOARD.Tests.Application.Invitations.Commands;

/// <summary>Unit tests for <see cref="CreateInvitationCommandHandler"/>.</summary>
public sealed class CreateInvitationCommandHandlerTests : IDisposable
{
    private const string RawToken  = "raw-invite-token";
    private const string TokenHash = "hashed-invite-token";
    private const string Email     = "invitee@external.local";

    private readonly TestDatabase                    _database;
    private readonly TestApplicationDbContext        _db;
    private readonly Mock<IInvitationTokenService>   _tokens    = new();
    private readonly Mock<IPublishEndpoint>          _publisher = new();
    private readonly Mock<IAppSettings>              _settings  = new();
    private readonly Guid                            _repositoryId = Guid.NewGuid();
    private readonly Guid                            _inviterId    = Guid.NewGuid();

    /// <summary>Sets up an isolated database holding one active repository.</summary>
    public CreateInvitationCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;

        _db.Set<Repository>().Add(
            new Repository { Name = "Dashboard", Code = "DASH" }.WithId(_repositoryId));
        _db.SaveChanges();

        _tokens.Setup(t => t.Generate()).Returns((RawToken, TokenHash));
        _settings.SetupGet(s => s.InvitationTokenTtl).Returns(TimeSpan.FromHours(48));
        _settings.SetupGet(s => s.InvitationFrontendBaseUrl).Returns("https://app.test/");
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private CreateInvitationCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _tokens.Object, _publisher.Object, _settings.Object);

    private IRequestUserContext AuthorizedUser() =>
        RequestUserContextMock.ForUser(_inviterId)
            .WithPrivilege(SystemFunction.InviteMembers, _repositoryId).Object;

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

    private async Task<User> AddUserAsync(string email, Guid? id = null, bool isDeleted = false)
    {
        var user = new User
        {
            Name         = "Existing",
            Email        = email,
            PasswordHash = "hash",
            PasswordSalt = "salt",
            IsDeleted    = isDeleted,
            DeletedAt    = isDeleted ? DateTime.UtcNow : null,
        };
        if (id.HasValue) user.WithId(id.Value);

        _db.Set<User>().Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private CreateInvitationCommand Command(
        Guid roleId, string email = Email, Guid? managerId = null, Guid? repositoryId = null) =>
        new(repositoryId ?? _repositoryId, email, "Developer", roleId, managerId);

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutInviteMembersPrivilege_Fails()
    {
        var role    = await AddRoleAsync();
        var handler = CreateHandler(RequestUserContextMock.ForUser(_inviterId).Object);

        var result = await handler.Handle(Command(role.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("permission");
        (await _db.Set<Invitation>().CountAsync()).Should().Be(0);
    }

    // ── Repository and role validation ──────────────────────────────────────

    [Fact]
    public async Task Handle_WhenRepositoryDoesNotExist_Fails()
    {
        var role    = await AddRoleAsync(isDefault: true);
        var handler = CreateHandler(
            RequestUserContextMock.ForUser(_inviterId)
                .WithPrivilege(SystemFunction.InviteMembers).Object);

        var result = await handler.Handle(
            Command(role.Id, repositoryId: Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found or is archived");
    }

    [Fact]
    public async Task Handle_WhenRepositoryIsArchived_Fails()
    {
        var archivedId = Guid.NewGuid();
        _db.Set<Repository>().Add(
            new Repository { Name = "Old", Code = "OLD", IsArchived = true }.WithId(archivedId));
        await _db.SaveChangesAsync();

        var role    = await AddRoleAsync(isDefault: true);
        var handler = CreateHandler(
            RequestUserContextMock.ForUser(_inviterId)
                .WithPrivilege(SystemFunction.InviteMembers).Object);

        var result = await handler.Handle(
            Command(role.Id, repositoryId: archivedId), CancellationToken.None);

        result.IsFailure.Should().BeTrue("an archived repository takes no new invitations");
    }

    [Fact]
    public async Task Handle_WhenRoleDoesNotExist_Fails()
    {
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(Command(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Role not found");
    }

    [Fact]
    public async Task Handle_WhenRoleBelongsToAnotherRepository_Fails()
    {
        var foreignRole = await AddRoleAsync(repositoryId: Guid.NewGuid());
        var handler     = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(Command(foreignRole.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithAGlobalDefaultRole_Succeeds()
    {
        var role    = await AddRoleAsync(isDefault: true);
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(Command(role.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // ── Existing-account rule ───────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheEmailAlreadyHasAnAccount_Fails()
    {
        await AddUserAsync(Email);
        var role    = await AddRoleAsync();
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(Command(role.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This email address is not eligible for an invitation.",
            "the message deliberately avoids confirming that the address is registered");
    }

    // ── Manager validation ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheManagerDoesNotExist_Fails()
    {
        var role    = await AddRoleAsync();
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(role.Id, managerId: Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("manager does not exist");
    }

    [Fact]
    public async Task Handle_AcceptsADeactivatedUserAsManager()
    {
        var manager = await AddUserAsync("manager@test.local", isDeleted: true);
        var role    = await AddRoleAsync();
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(role.Id, managerId: manager.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(
            "the manager lookup uses IgnoreQueryFilters");
    }

    // ── Prior-invite revocation ─────────────────────────────────────────────

    [Fact]
    public async Task Handle_RevokesPriorPendingInvitesForTheSameEmailAndRepository()
    {
        var role = await AddRoleAsync();
        _db.Set<Invitation>().Add(new Invitation
        {
            Email           = Email,
            RepositoryId    = _repositoryId,
            InvitedByUserId = _inviterId,
            RoleId          = role.Id,
            TokenHash       = "older-hash",
            ExpiresAt       = DateTime.UtcNow.AddHours(24),
            Status          = InvitationStatus.Pending,
        });
        await _db.SaveChangesAsync();

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(Command(role.Id), CancellationToken.None);

        var older = await _db.Set<Invitation>().AsNoTracking()
            .SingleAsync(i => i.TokenHash == "older-hash");
        older.Status.Should().Be(InvitationStatus.Revoked,
            "only the newest invite link may stay usable");
    }

    [Fact]
    public async Task Handle_DoesNotRevokeInvitesForOtherRepositories()
    {
        var role = await AddRoleAsync();
        _db.Set<Invitation>().Add(new Invitation
        {
            Email           = Email,
            RepositoryId    = Guid.NewGuid(),
            InvitedByUserId = _inviterId,
            RoleId          = role.Id,
            TokenHash       = "other-repo-hash",
            ExpiresAt       = DateTime.UtcNow.AddHours(24),
            Status          = InvitationStatus.Pending,
        });
        await _db.SaveChangesAsync();

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(Command(role.Id), CancellationToken.None);

        var other = await _db.Set<Invitation>().AsNoTracking()
            .SingleAsync(i => i.TokenHash == "other-repo-hash");
        other.Status.Should().Be(InvitationStatus.Pending);
    }

    // ── Persistence ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_StoresOnlyTheTokenHash()
    {
        var role    = await AddRoleAsync();
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(Command(role.Id), CancellationToken.None);

        var invitation = await _db.Set<Invitation>().AsNoTracking().SingleAsync();
        invitation.TokenHash.Should().Be(TokenHash);
        invitation.TokenHash.Should().NotBe(RawToken, "the raw token lives only in the email link");
    }

    [Fact]
    public async Task Handle_SetsTheExpiryFromTheConfiguredTtl()
    {
        var role    = await AddRoleAsync();
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(Command(role.Id), CancellationToken.None);

        result.Value!.ExpiresAt.Should()
              .BeCloseTo(DateTime.UtcNow.AddHours(48), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Handle_RecordsTheInviterAndChosenRole()
    {
        var role    = await AddRoleAsync();
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(Command(role.Id), CancellationToken.None);

        var invitation = await _db.Set<Invitation>().AsNoTracking().SingleAsync();
        invitation.InvitedByUserId.Should().Be(_inviterId);
        invitation.RoleId.Should().Be(role.Id);
        invitation.DefaultRole.Should().Be("Developer");
        invitation.Status.Should().Be(InvitationStatus.Pending);
    }

    // ── Email dispatch ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_PublishesAnInviteEmailCarryingTheRawTokenInTheUrlFragment()
    {
        await AddUserAsync("inviter@test.local", id: _inviterId);
        var role    = await AddRoleAsync();
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(Command(role.Id), CancellationToken.None);

        _publisher.Verify(p => p.Publish(
            It.Is<InvitationCreatedMessage>(m =>
                m.ToEmail == Email
             && m.InviteLink == $"https://app.test/invite/accept#{RawToken}"
             && m.RepositoryName == "Dashboard"
             && m.ExpiryMinutes == 48 * 60),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTheInviterHasNoUserRow_FallsBackToAGenericName()
    {
        var role    = await AddRoleAsync();
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(Command(role.Id), CancellationToken.None);

        _publisher.Verify(p => p.Publish(
            It.Is<InvitationCreatedMessage>(m => m.InvitedByName == "A team member"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
