using DASHBOARD.Application.Auth.Commands.Login;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Infrastructure.Identity;
using DASHBOARD.UnitTests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace DASHBOARD.UnitTests.Application.Auth.Commands;

/// <summary>
/// Unit tests for <see cref="LoginCommandHandler"/>.
/// </summary>
/// <remarks>
/// BUG-002 (High): this handler threw <c>UnauthorizedAccessException</c> for a wrong password —
/// the single most routine outcome an auth endpoint has — which broke a debugger on every
/// mistyped login. Every failure test here asserts a <c>Result.Failure</c>, never an exception.
/// </remarks>
public sealed class LoginCommandHandlerTests : IDisposable
{
    private const string Alias        = "acme";
    private const string Email        = "user@acme.local";
    private const string Password     = "Str0ng!Pass";
    private const string StoredHash   = "stored-hash";
    private const string StoredSalt   = "stored-salt";
    private const string RefreshToken = "plaintext-refresh-token";

    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Mock<IPasswordService>   _passwords = new();
    private readonly Mock<ITokenService>      _tokens    = new();
    private readonly Guid                     _orgId     = Guid.NewGuid();
    private readonly DateTime                 _accessExpiry = DateTime.UtcNow.AddHours(8);

    /// <summary>Sets up an isolated database with a password service that rejects by default.</summary>
    public LoginCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;

        _passwords.Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(false);
        _tokens.Setup(t => t.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>()))
               .Returns(new TokenResult("signed.jwt.value", "jwt-id-1", _accessExpiry));
        _tokens.Setup(t => t.GenerateRefreshToken()).Returns(RefreshToken);
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private LoginCommandHandler CreateHandler(int refreshTokenDays = 7) =>
        new(_db, _passwords.Object, _tokens.Object, _database.Uow,
            Options.Create(new JwtSettings { RefreshTokenExpiresInDays = refreshTokenDays }));

    /// <summary>Configures the password service to accept exactly the seeded credentials.</summary>
    private void AcceptPassword(string plaintext = Password) =>
        _passwords.Setup(p => p.VerifyPassword(plaintext, StoredHash, StoredSalt)).Returns(true);

    private async Task<Organization> AddOrgAsync(string alias = Alias)
    {
        var org = new Organization
        {
            Name         = alias.ToUpperInvariant(),
            Alias        = alias,
            ContactEmail = $"admin@{alias}.local",
            LicenseKey   = "LICENSE",
        };
        if (alias == Alias) org.WithId(_orgId);

        _db.Set<Organization>().Add(org);
        await _db.SaveChangesAsync();
        return org;
    }

    private async Task<User> AddUserAsync(
        Guid? orgId = null, string email = Email, bool isGlobalAdmin = false)
    {
        var user = new User
        {
            OrgId         = orgId ?? _orgId,
            Name          = "Test User",
            Email         = email,
            PasswordHash  = StoredHash,
            PasswordSalt  = StoredSalt,
            IsGlobalAdmin = isGlobalAdmin,
            AvatarClass   = "bg-sky-600",
        };
        _db.Set<User>().Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    // ── BUG-002 regression: failures are results, not exceptions ────────────

    [Fact]
    public async Task Handle_WithWrongPassword_FailsWithoutThrowing()
    {
        await AddOrgAsync();
        await AddUserAsync();
        AcceptPassword();

        var handler = CreateHandler();

        var result = await handler.Handle(
            new LoginCommand(Alias, Email, "WrongPassword!"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        (await _db.Set<UserToken>().CountAsync()).Should().Be(0, "no session may be issued");
    }

    [Fact]
    public async Task Handle_WithUnknownEmail_FailsWithoutThrowing()
    {
        await AddOrgAsync();
        var handler = CreateHandler();

        var result = await handler.Handle(
            new LoginCommand(Alias, "nobody@acme.local", Password), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithUnknownOrganization_FailsWithoutThrowing()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(
            new LoginCommand("nosuchorg", Email, Password), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    // ── Anti-enumeration ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsTheSameMessageForEveryKindOfFailure()
    {
        await AddOrgAsync();
        await AddUserAsync();
        AcceptPassword();

        var handler = CreateHandler();

        var unknownOrg      = await handler.Handle(
            new LoginCommand("nosuchorg", Email, Password), CancellationToken.None);
        var unknownEmail    = await handler.Handle(
            new LoginCommand(Alias, "nobody@acme.local", Password), CancellationToken.None);
        var wrongPassword   = await handler.Handle(
            new LoginCommand(Alias, Email, "WrongPassword!"), CancellationToken.None);

        // Distinct messages would let an attacker probe which organizations and emails exist.
        unknownEmail.Error.Should().Be(unknownOrg.Error);
        wrongPassword.Error.Should().Be(unknownOrg.Error);
        unknownOrg.Error.Should().Be("Invalid organization, email or password.");
    }

    // ── Multi-tenancy ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheEmailBelongsToAnotherOrganization_Fails()
    {
        await AddOrgAsync();
        var otherOrg = await AddOrgAsync("globex");
        await AddUserAsync(orgId: otherOrg.Id);
        AcceptPassword();

        var handler = CreateHandler();

        // Right credentials, wrong tenant.
        var result = await handler.Handle(
            new LoginCommand(Alias, Email, Password), CancellationToken.None);

        result.IsFailure.Should().BeTrue("a user may only sign in through their own organization");
    }

    [Fact]
    public async Task Handle_AllowsTheSameEmailInDifferentOrganizations()
    {
        await AddOrgAsync();
        var otherOrg = await AddOrgAsync("globex");
        await AddUserAsync();
        var globexUser = await AddUserAsync(orgId: otherOrg.Id);
        AcceptPassword();

        var handler = CreateHandler();

        var result = await handler.Handle(
            new LoginCommand("globex", Email, Password), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.UserId.Should().Be(globexUser.Id);
        result.Value.OrgId.Should().Be(otherOrg.Id);
    }

    // ── Input normalisation ─────────────────────────────────────────────────

    [Theory]
    [InlineData("ACME")]
    [InlineData("  acme  ")]
    [InlineData("  AcMe ")]
    public async Task Handle_NormalisesTheOrganizationAlias(string alias)
    {
        await AddOrgAsync();
        await AddUserAsync();
        AcceptPassword();

        var handler = CreateHandler();

        var result = await handler.Handle(
            new LoginCommand(alias, Email, Password), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MatchesTheEmailCaseInsensitively()
    {
        await AddOrgAsync();
        await AddUserAsync();
        AcceptPassword();

        var handler = CreateHandler();

        var result = await handler.Handle(
            new LoginCommand(Alias, "USER@ACME.LOCAL", Password), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // ── Successful sign-in ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidCredentials_ReturnsTheUserAndOrganization()
    {
        await AddOrgAsync();
        var user = await AddUserAsync(isGlobalAdmin: true);
        AcceptPassword();

        var handler = CreateHandler();

        var result = await handler.Handle(
            new LoginCommand(Alias, Email, Password), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.UserId.Should().Be(user.Id);
        result.Value.Name.Should().Be("Test User");
        result.Value.Email.Should().Be(Email);
        result.Value.IsGlobalAdmin.Should().BeTrue();
        result.Value.OrgId.Should().Be(_orgId);
        result.Value.OrgAlias.Should().Be(Alias);
        result.Value.AvatarClass.Should().Be("bg-sky-600");
    }

    [Fact]
    public async Task Handle_ReturnsTheGeneratedTokenPair()
    {
        await AddOrgAsync();
        await AddUserAsync();
        AcceptPassword();

        var handler = CreateHandler();

        var result = await handler.Handle(
            new LoginCommand(Alias, Email, Password), CancellationToken.None);

        result.Value!.AccessToken.Should().Be("signed.jwt.value");
        result.Value.JwtId.Should().Be("jwt-id-1");
        result.Value.AccessTokenExpiresAt.Should().Be(_accessExpiry);
        result.Value.RefreshToken.Should().Be(RefreshToken);
    }

    [Fact]
    public async Task Handle_IssuesTheAccessTokenForTheUsersOwnOrganization()
    {
        await AddOrgAsync();
        var user = await AddUserAsync();
        AcceptPassword();

        var handler = CreateHandler();

        await handler.Handle(new LoginCommand(Alias, Email, Password), CancellationToken.None);

        _tokens.Verify(t => t.GenerateToken(user.Id, Email, "Test User", _orgId), Times.Once);
    }

    // ── Token persistence ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_StoresOnlyTheHashOfTheRefreshToken()
    {
        await AddOrgAsync();
        await AddUserAsync();
        AcceptPassword();

        var handler = CreateHandler();

        await handler.Handle(new LoginCommand(Alias, Email, Password), CancellationToken.None);

        var stored = await _db.Set<UserToken>().AsNoTracking().SingleAsync();
        stored.RefreshTokenHash.Should().NotBe(RefreshToken, "the plaintext must never be persisted");
        stored.RefreshTokenHash.Should().Be(LoginCommandHandler.HashToken(RefreshToken));
    }

    [Fact]
    public async Task Handle_PersistsTheSessionUnrevoked()
    {
        await AddOrgAsync();
        var user = await AddUserAsync();
        AcceptPassword();

        var handler = CreateHandler();

        await handler.Handle(new LoginCommand(Alias, Email, Password), CancellationToken.None);

        var stored = await _db.Set<UserToken>().AsNoTracking().SingleAsync();
        stored.UserId.Should().Be(user.Id);
        stored.JwtId.Should().Be("jwt-id-1");
        stored.AccessTokenExpiresAt.Should().Be(_accessExpiry);
        stored.IsRevoked.Should().BeFalse();
        stored.RefreshTokenIsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_SetsTheRefreshExpiryFromConfiguration()
    {
        await AddOrgAsync();
        await AddUserAsync();
        AcceptPassword();

        var handler = CreateHandler(refreshTokenDays: 30);

        var result = await handler.Handle(
            new LoginCommand(Alias, Email, Password), CancellationToken.None);

        result.Value!.RefreshTokenExpiresAt.Should()
              .BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Handle_SigningInTwiceIssuesTwoSessions()
    {
        await AddOrgAsync();
        await AddUserAsync();
        AcceptPassword();

        var handler = CreateHandler();

        await handler.Handle(new LoginCommand(Alias, Email, Password), CancellationToken.None);
        await handler.Handle(new LoginCommand(Alias, Email, Password), CancellationToken.None);

        (await _db.Set<UserToken>().CountAsync()).Should()
            .Be(2, "signing in on a second device must not invalidate the first");
    }
}
