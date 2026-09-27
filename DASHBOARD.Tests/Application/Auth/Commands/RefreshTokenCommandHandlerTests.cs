using DASHBOARD.Application.Auth.Commands.Login;
using DASHBOARD.Application.Auth.Commands.RefreshToken;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Infrastructure.Identity;
using DASHBOARD.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace DASHBOARD.Tests.Application.Auth.Commands;

/// <summary>Unit tests for <see cref="RefreshTokenCommandHandler"/> — token rotation.</summary>
public sealed class RefreshTokenCommandHandlerTests : IDisposable
{
    private const string CurrentRefresh = "current-refresh-token";
    private const string RotatedRefresh = "rotated-refresh-token";

    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Mock<ITokenService>      _tokens = new();
    private readonly Guid                     _orgId  = Guid.NewGuid();
    private readonly DateTime                 _accessExpiry = DateTime.UtcNow.AddHours(8);

    /// <summary>Sets up an isolated database and a token service returning fixed values.</summary>
    public RefreshTokenCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;

        _tokens.Setup(t => t.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>()))
               .Returns(new TokenResult("new.jwt.value", "jwt-id-2", _accessExpiry));
        _tokens.Setup(t => t.GenerateRefreshToken()).Returns(RotatedRefresh);
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private RefreshTokenCommandHandler CreateHandler(int refreshTokenDays = 7) =>
        new(_db, _tokens.Object, _database.Uow,
            Options.Create(new JwtSettings { RefreshTokenExpiresInDays = refreshTokenDays }));

    private async Task<Organization> AddOrgAsync(string alias = "acme")
    {
        var org = new Organization
        {
            Name         = "Acme",
            Alias        = alias,
            ContactEmail = "admin@acme.local",
            LicenseKey   = "LICENSE",
        }.WithId(_orgId);

        _db.Set<Organization>().Add(org);
        await _db.SaveChangesAsync();
        return org;
    }

    private async Task<User> AddUserAsync()
    {
        var user = new User
        {
            OrgId        = _orgId,
            Name         = "Test User",
            Email        = "user@acme.local",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            AvatarClass  = "bg-sky-600",
        };
        _db.Set<User>().Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<UserToken> AddSessionAsync(
        Guid      userId,
        string    refreshToken   = CurrentRefresh,
        bool      refreshRevoked = false,
        DateTime? refreshExpiry  = null)
    {
        var token = new UserToken
        {
            UserId                = userId,
            JwtId                 = "jwt-id-1",
            AccessTokenExpiresAt  = DateTime.UtcNow.AddHours(8),
            IsRevoked             = false,
            RefreshTokenHash      = LoginCommandHandler.HashToken(refreshToken),
            RefreshTokenExpiresAt = refreshExpiry ?? DateTime.UtcNow.AddDays(7),
            RefreshTokenIsRevoked = refreshRevoked,
        };
        _db.Set<UserToken>().Add(token);
        await _db.SaveChangesAsync();
        return token;
    }

    // ── Rejection paths ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheTokenIsUnknown_Throws()
    {
        await AddOrgAsync();
        await AddUserAsync();
        var handler = CreateHandler();

        var act = () => handler.Handle(
            new RefreshTokenCommand("never-issued"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_WhenTheTokenIsAlreadyRevoked_Throws()
    {
        await AddOrgAsync();
        var user = await AddUserAsync();
        await AddSessionAsync(user.Id, refreshRevoked: true);

        var handler = CreateHandler();

        var act = () => handler.Handle(
            new RefreshTokenCommand(CurrentRefresh), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_WhenTheTokenHasExpired_Throws()
    {
        await AddOrgAsync();
        var user = await AddUserAsync();
        await AddSessionAsync(user.Id, refreshExpiry: DateTime.UtcNow.AddMinutes(-1));

        var handler = CreateHandler();

        var act = () => handler.Handle(
            new RefreshTokenCommand(CurrentRefresh), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_ReportsTheSameMessageForEveryRejection()
    {
        await AddOrgAsync();
        var user = await AddUserAsync();
        await AddSessionAsync(user.Id, refreshRevoked: true);

        var handler = CreateHandler();

        var unknown = await handler.Invoking(h => h.Handle(
            new RefreshTokenCommand("never-issued"), CancellationToken.None))
            .Should().ThrowAsync<UnauthorizedAccessException>();
        var revoked = await handler.Invoking(h => h.Handle(
            new RefreshTokenCommand(CurrentRefresh), CancellationToken.None))
            .Should().ThrowAsync<UnauthorizedAccessException>();

        revoked.Which.Message.Should().Be(unknown.Which.Message,
            "distinguishing the two would let an attacker enumerate valid tokens");
    }

    // ── Rotation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_RevokesTheSuppliedTokenAndIssuesANewOne()
    {
        await AddOrgAsync();
        var user    = await AddUserAsync();
        var session = await AddSessionAsync(user.Id);

        var handler = CreateHandler();

        await handler.Handle(new RefreshTokenCommand(CurrentRefresh), CancellationToken.None);

        var oldSession = await _db.Set<UserToken>().AsNoTracking().SingleAsync(t => t.Id == session.Id);
        oldSession.IsRevoked.Should().BeTrue();
        oldSession.RefreshTokenIsRevoked.Should().BeTrue();
        oldSession.RevokedAt.Should().NotBeNull();
        oldSession.RefreshTokenRevokedAt.Should().NotBeNull();

        (await _db.Set<UserToken>().CountAsync()).Should().Be(2, "the replacement session is added");
    }

    [Fact]
    public async Task Handle_TheSameTokenCannotBeUsedTwice()
    {
        await AddOrgAsync();
        var user = await AddUserAsync();
        await AddSessionAsync(user.Id);

        var handler = CreateHandler();

        await handler.Handle(new RefreshTokenCommand(CurrentRefresh), CancellationToken.None);

        var act = () => handler.Handle(new RefreshTokenCommand(CurrentRefresh), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>(
            "a rotated token is spent — replaying it must fail");
    }

    [Fact]
    public async Task Handle_StoresOnlyTheHashOfTheRotatedToken()
    {
        await AddOrgAsync();
        var user    = await AddUserAsync();
        var session = await AddSessionAsync(user.Id);

        var handler = CreateHandler();

        await handler.Handle(new RefreshTokenCommand(CurrentRefresh), CancellationToken.None);

        var newSession = await _db.Set<UserToken>().AsNoTracking()
            .SingleAsync(t => t.Id != session.Id);
        newSession.RefreshTokenHash.Should().NotBe(RotatedRefresh);
        newSession.RefreshTokenHash.Should().Be(LoginCommandHandler.HashToken(RotatedRefresh));
        newSession.RefreshTokenIsRevoked.Should().BeFalse();
        newSession.IsRevoked.Should().BeFalse();
    }

    // ── Returned payload ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsTheNewTokenPair()
    {
        await AddOrgAsync();
        var user = await AddUserAsync();
        await AddSessionAsync(user.Id);

        var handler = CreateHandler();

        var result = await handler.Handle(
            new RefreshTokenCommand(CurrentRefresh), CancellationToken.None);

        result.AccessToken.Should().Be("new.jwt.value");
        result.JwtId.Should().Be("jwt-id-2");
        result.RefreshToken.Should().Be(RotatedRefresh);
        result.RefreshToken.Should().NotBe(CurrentRefresh, "both halves of the pair rotate");
    }

    [Fact]
    public async Task Handle_CarriesTheUserAndOrganizationIdentity()
    {
        var org  = await AddOrgAsync("globex");
        var user = await AddUserAsync();
        await AddSessionAsync(user.Id);

        var handler = CreateHandler();

        var result = await handler.Handle(
            new RefreshTokenCommand(CurrentRefresh), CancellationToken.None);

        result.UserId.Should().Be(user.Id);
        result.Email.Should().Be("user@acme.local");
        result.Name.Should().Be("Test User");
        result.OrgId.Should().Be(org.Id);
        result.OrgAlias.Should().Be("globex");
        result.AvatarClass.Should().Be("bg-sky-600");
    }

    [Fact]
    public async Task Handle_WhenTheOrganizationIsMissing_ReturnsAnEmptyAlias()
    {
        // No Organization row — the handler degrades to an empty alias rather than failing.
        var user = await AddUserAsync();
        await AddSessionAsync(user.Id);

        var handler = CreateHandler();

        var result = await handler.Handle(
            new RefreshTokenCommand(CurrentRefresh), CancellationToken.None);

        result.OrgAlias.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_SetsTheRefreshExpiryFromConfiguration()
    {
        await AddOrgAsync();
        var user = await AddUserAsync();
        await AddSessionAsync(user.Id);

        var handler = CreateHandler(refreshTokenDays: 30);

        var result = await handler.Handle(
            new RefreshTokenCommand(CurrentRefresh), CancellationToken.None);

        result.RefreshTokenExpiresAt.Should()
              .BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Handle_DoesNotTouchOtherSessionsOfTheSameUser()
    {
        await AddOrgAsync();
        var user       = await AddUserAsync();
        await AddSessionAsync(user.Id);
        var otherDevice = await AddSessionAsync(user.Id, refreshToken: "other-device-token");

        var handler = CreateHandler();

        await handler.Handle(new RefreshTokenCommand(CurrentRefresh), CancellationToken.None);

        var untouched = await _db.Set<UserToken>().AsNoTracking()
            .SingleAsync(t => t.Id == otherDevice.Id);
        untouched.RefreshTokenIsRevoked.Should().BeFalse(
            "refreshing on one device must not sign the user out everywhere else");
    }
}
