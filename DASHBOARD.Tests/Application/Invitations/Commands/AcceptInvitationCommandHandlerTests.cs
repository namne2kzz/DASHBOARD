using DASHBOARD.Application.Auth.Commands.Login;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Invitations.Commands.AcceptInvitation;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Infrastructure.Identity;
using DASHBOARD.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace DASHBOARD.Tests.Application.Invitations.Commands;

/// <summary>Unit tests for <see cref="AcceptInvitationCommandHandler"/>.</summary>
public sealed class AcceptInvitationCommandHandlerTests : IDisposable
{
    private const string RawToken      = "raw-invite-token";
    private const string TokenHash     = "hashed-invite-token";
    private const string InvitedEmail  = "invitee@external.local";
    private const string GoogleIdToken = "google-id-token";
    private const string GoogleSubject = "google-subject-123";
    private const string RefreshToken  = "plaintext-refresh";

    private readonly TestDatabase                  _database;
    private readonly TestApplicationDbContext      _db;
    private readonly Mock<IInvitationTokenService> _tokens     = new();
    private readonly Mock<IGoogleAuthService>      _google     = new();
    private readonly Mock<ITokenService>           _jwt        = new();
    private readonly Mock<IAppSettings>            _settings   = new();
    private readonly Guid                          _orgId        = Guid.NewGuid();
    private readonly Guid                          _repositoryId = Guid.NewGuid();
    private readonly DateTime                      _accessExpiry = DateTime.UtcNow.AddHours(8);

    /// <summary>Sets up an isolated database with an organization, repository and default role.</summary>
    public AcceptInvitationCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;

        _db.Set<Organization>().Add(new Organization
        {
            Name = "Acme", Alias = "acme", ContactEmail = "admin@acme.local", LicenseKey = "L",
        }.WithId(_orgId));
        _db.Set<Repository>().Add(
            new Repository { OrgId = _orgId, Name = "Dashboard", Code = "DASH" }.WithId(_repositoryId));
        _db.SaveChanges();

        _tokens.Setup(t => t.Hash(RawToken)).Returns(TokenHash);
        _google.Setup(g => g.ValidateAsync(GoogleIdToken, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new GoogleUserInfo(GoogleSubject, InvitedEmail, "Invited Person", null));
        _jwt.Setup(t => t.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>()))
            .Returns(new TokenResult("signed.jwt", "jwt-id-1", _accessExpiry));
        _jwt.Setup(t => t.GenerateRefreshToken()).Returns(RefreshToken);
        _settings.SetupGet(s => s.DefaultGoogleUserAvatarClass).Returns("bg-emerald-600");
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private AcceptInvitationCommandHandler CreateHandler() =>
        new(_db, _tokens.Object, _google.Object, _jwt.Object,
            Options.Create(new JwtSettings { RefreshTokenExpiresInDays = 7 }), _settings.Object);

    private async Task<Role> AddRoleAsync(Guid? repositoryId = null)
    {
        var role = new Role
        {
            Name             = "Developer",
            RepositoryId     = repositoryId ?? _repositoryId,
            AllowedFunctions = [SystemFunction.ViewRepository],
        };
        _db.Set<Role>().Add(role);
        await _db.SaveChangesAsync();
        return role;
    }

    private async Task<Invitation> AddInvitationAsync(
        Guid              roleId,
        string            email     = InvitedEmail,
        InvitationStatus  status    = InvitationStatus.Pending,
        DateTime?         expiresAt = null,
        Guid?             managerId = null,
        string            tokenHash = TokenHash)
    {
        var invitation = new Invitation
        {
            Email           = email,
            RepositoryId    = _repositoryId,
            InvitedByUserId = Guid.NewGuid(),
            RoleId          = roleId,
            DefaultRole     = "Developer",
            ManagerId       = managerId,
            TokenHash       = tokenHash,
            ExpiresAt       = expiresAt ?? DateTime.UtcNow.AddHours(24),
            Status          = status,
        };
        _db.Set<Invitation>().Add(invitation);
        await _db.SaveChangesAsync();
        return invitation;
    }

    private static AcceptInvitationCommand Command() => new(RawToken, GoogleIdToken);

    // ── Token resolution ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheTokenIsUnknown_Fails()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found or already used");
    }

    [Theory]
    [InlineData(InvitationStatus.Accepted)]
    [InlineData(InvitationStatus.Revoked)]
    [InlineData(InvitationStatus.Expired)]
    public async Task Handle_WhenTheInvitationIsNoLongerPending_Fails(InvitationStatus status)
    {
        var role = await AddRoleAsync();
        await AddInvitationAsync(role.Id, status: status);

        var handler = CreateHandler();

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue("a spent or withdrawn invite must not be replayable");
        (await _db.Set<User>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheInvitationHasExpired_FailsAndMarksItExpired()
    {
        var role       = await AddRoleAsync();
        var invitation = await AddInvitationAsync(role.Id, expiresAt: DateTime.UtcNow.AddMinutes(-1));

        var handler = CreateHandler();

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("expired");

        var stored = await _db.Set<Invitation>().AsNoTracking().SingleAsync(i => i.Id == invitation.Id);
        stored.Status.Should().Be(InvitationStatus.Expired,
            "the row is updated so the same link stops being probed as Pending");
    }

    // ── Google identity ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenGoogleValidationThrows_Fails()
    {
        var role = await AddRoleAsync();
        await AddInvitationAsync(role.Id);

        _google.Setup(g => g.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new InvalidOperationException("bad token"));

        var handler = CreateHandler();

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Google token verification failed");
    }

    [Fact]
    public async Task Handle_WhenTheGoogleEmailDoesNotMatchTheInvite_Fails()
    {
        var role = await AddRoleAsync();
        await AddInvitationAsync(role.Id, email: "someone.else@external.local");

        var handler = CreateHandler();

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue(
            "an invite link must only work for the address it was issued to");
        result.Error.Should().Contain("does not match the invited email");
        (await _db.Set<User>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_MatchesTheInvitedEmailCaseInsensitively()
    {
        var role = await AddRoleAsync();
        await AddInvitationAsync(role.Id, email: "INVITEE@EXTERNAL.LOCAL");

        var handler = CreateHandler();

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // ── Role still valid ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheAssignedRoleWasDeleted_Fails()
    {
        // The invite still names a role id, but no such role remains for this repository —
        // what an admin deleting a custom role after sending the invite leaves behind.
        await AddInvitationAsync(Guid.NewGuid());

        var handler = CreateHandler();

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("no longer exists");
        (await _db.Set<RepositoryMember>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheAssignedRoleBelongsToAnotherRepository_Fails()
    {
        var foreignRole = await AddRoleAsync(repositoryId: Guid.NewGuid());
        await AddInvitationAsync(foreignRole.Id);

        var handler = CreateHandler();

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue(
            "a custom role only counts for the repository that owns it");
    }

    // ── New account creation ────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CreatesAGoogleOnlyAccountInTheRepositorysOrganization()
    {
        var role = await AddRoleAsync();
        await AddInvitationAsync(role.Id);

        var handler = CreateHandler();

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var created = await _db.Set<User>().AsNoTracking().SingleAsync();
        created.OrgId.Should().Be(_orgId, "the invitee joins the repository's tenant");
        created.Email.Should().Be(InvitedEmail);
        created.Name.Should().Be("Invited Person");
        created.AuthProvider.Should().Be(AuthProvider.Google);
        created.GoogleSubjectId.Should().Be(GoogleSubject);
        created.PasswordHash.Should().BeEmpty("an invited user has no password to sign in with");
        created.PasswordSalt.Should().BeEmpty();
        created.AvatarClass.Should().Be("bg-emerald-600");
    }

    [Fact]
    public async Task Handle_WhenGoogleReturnsNoName_FallsBackToTheEmail()
    {
        _google.Setup(g => g.ValidateAsync(GoogleIdToken, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new GoogleUserInfo(GoogleSubject, InvitedEmail, null, null));

        var role = await AddRoleAsync();
        await AddInvitationAsync(role.Id);

        var handler = CreateHandler();

        await handler.Handle(Command(), CancellationToken.None);

        (await _db.Set<User>().AsNoTracking().SingleAsync()).Name.Should().Be(InvitedEmail);
    }

    [Fact]
    public async Task Handle_AppliesTheManagerChosenByTheInviter()
    {
        var manager = new User
        {
            OrgId = _orgId, Name = "Manager", Email = "mgr@acme.local",
            PasswordHash = "h", PasswordSalt = "s",
        };
        _db.Set<User>().Add(manager);
        await _db.SaveChangesAsync();

        var role = await AddRoleAsync();
        await AddInvitationAsync(role.Id, managerId: manager.Id);

        var handler = CreateHandler();

        await handler.Handle(Command(), CancellationToken.None);

        var created = await _db.Set<User>().AsNoTracking()
            .SingleAsync(u => u.GoogleSubjectId == GoogleSubject);
        created.ManagerId.Should().Be(manager.Id);
    }

    // ── Existing account ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheGoogleAccountAlreadyExists_ReusesItInsteadOfCreatingAnother()
    {
        var existing = new User
        {
            OrgId           = _orgId,
            Name            = "Already Here",
            Email           = InvitedEmail,
            AuthProvider    = AuthProvider.Google,
            GoogleSubjectId = GoogleSubject,
            PasswordHash    = string.Empty,
            PasswordSalt    = string.Empty,
        };
        _db.Set<User>().Add(existing);
        await _db.SaveChangesAsync();

        var role = await AddRoleAsync();
        await AddInvitationAsync(role.Id);

        var handler = CreateHandler();

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.Value!.UserId.Should().Be(existing.Id);
        result.Value.Name.Should().Be("Already Here");
        (await _db.Set<User>().CountAsync()).Should().Be(1, "a second invite adds no duplicate account");
    }

    [Fact]
    public async Task Handle_WhenTheUserIsAlreadyAMember_DoesNotAddASecondMembership()
    {
        var existing = new User
        {
            OrgId = _orgId, Name = "Already Here", Email = InvitedEmail,
            AuthProvider = AuthProvider.Google, GoogleSubjectId = GoogleSubject,
            PasswordHash = string.Empty, PasswordSalt = string.Empty,
        };
        _db.Set<User>().Add(existing);
        await _db.SaveChangesAsync();

        var role = await AddRoleAsync();
        _db.Set<RepositoryMember>().Add(new RepositoryMember
        {
            UserId = existing.Id, RepositoryId = _repositoryId,
            RoleId = role.Id, DefaultRole = "Developer",
        });
        await _db.SaveChangesAsync();

        await AddInvitationAsync(role.Id);

        var handler = CreateHandler();

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _db.Set<RepositoryMember>().CountAsync()).Should().Be(1);
    }

    // ── Membership and invitation state ─────────────────────────────────────

    [Fact]
    public async Task Handle_AddsTheMembershipWithTheInvitedRole()
    {
        var role = await AddRoleAsync();
        await AddInvitationAsync(role.Id);

        var handler = CreateHandler();

        await handler.Handle(Command(), CancellationToken.None);

        var membership = await _db.Set<RepositoryMember>().AsNoTracking().SingleAsync();
        membership.RepositoryId.Should().Be(_repositoryId);
        membership.RoleId.Should().Be(role.Id);
        membership.DefaultRole.Should().Be("Developer");
    }

    [Fact]
    public async Task Handle_MarksTheInvitationAcceptedSoItCannotBeReused()
    {
        var role       = await AddRoleAsync();
        var invitation = await AddInvitationAsync(role.Id);

        var handler = CreateHandler();

        await handler.Handle(Command(), CancellationToken.None);

        var stored = await _db.Set<Invitation>().AsNoTracking().SingleAsync(i => i.Id == invitation.Id);
        stored.Status.Should().Be(InvitationStatus.Accepted);
        stored.AcceptedAt.Should().NotBeNull();

        var second = await handler.Handle(Command(), CancellationToken.None);
        second.IsFailure.Should().BeTrue("the link is one-time use");
    }

    // ── Session issued ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_IssuesASignedInSession()
    {
        var role = await AddRoleAsync();
        await AddInvitationAsync(role.Id);

        var handler = CreateHandler();

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.Value!.AccessToken.Should().Be("signed.jwt");
        result.Value.RefreshToken.Should().Be(RefreshToken);
        result.Value.OrgId.Should().Be(_orgId);
        result.Value.OrgAlias.Should().Be("acme");

        var session = await _db.Set<UserToken>().AsNoTracking().SingleAsync();
        session.JwtId.Should().Be("jwt-id-1");
        session.RefreshTokenHash.Should().Be(LoginCommandHandler.HashToken(RefreshToken),
            "the plaintext refresh token is never stored");
        session.IsRevoked.Should().BeFalse();
    }
}
