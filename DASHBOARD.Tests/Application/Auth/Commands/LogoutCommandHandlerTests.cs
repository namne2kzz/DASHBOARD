using DASHBOARD.Application.Auth.Commands.Login;
using DASHBOARD.Application.Auth.Commands.Logout;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Tests.Application.Auth.Commands;

/// <summary>Unit tests for <see cref="LogoutCommandHandler"/>.</summary>
public sealed class LogoutCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _userId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public LogoutCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private LogoutCommandHandler CreateHandler() => new(_db, _database.Uow);

    private async Task<UserToken> AddSessionAsync(
        string jwtId = "jwt-id-1", Guid? userId = null, bool revoked = false)
    {
        var token = new UserToken
        {
            UserId                = userId ?? _userId,
            JwtId                 = jwtId,
            AccessTokenExpiresAt  = DateTime.UtcNow.AddHours(8),
            IsRevoked             = revoked,
            RefreshTokenHash      = LoginCommandHandler.HashToken($"refresh-for-{jwtId}"),
            RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(7),
            RefreshTokenIsRevoked = revoked,
        };
        _db.Set<UserToken>().Add(token);
        await _db.SaveChangesAsync();
        return token;
    }

    private Task<UserToken> LoadAsync(Guid id) =>
        _db.Set<UserToken>().AsNoTracking().SingleAsync(t => t.Id == id);

    // ── Revocation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_RevokesBothHalvesOfTheSession()
    {
        var session = await AddSessionAsync();
        var handler = CreateHandler();

        await handler.Handle(new LogoutCommand("jwt-id-1", _userId), CancellationToken.None);

        var revoked = await LoadAsync(session.Id);
        revoked.IsRevoked.Should().BeTrue();
        revoked.RefreshTokenIsRevoked.Should().BeTrue(
            "leaving the refresh token alive would let the session be resurrected");
        revoked.RevokedAt.Should().NotBeNull();
        revoked.RefreshTokenRevokedAt.Should().NotBeNull();
    }

    // ── Tolerated no-ops ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheSessionDoesNotExist_SucceedsQuietly()
    {
        var handler = CreateHandler();

        var act = () => handler.Handle(
            new LogoutCommand("never-issued", _userId), CancellationToken.None);

        await act.Should().NotThrowAsync("logging out twice is not an error worth surfacing");
    }

    [Fact]
    public async Task Handle_WhenTheSessionIsAlreadyRevoked_StaysRevoked()
    {
        var session = await AddSessionAsync(revoked: true);
        var handler = CreateHandler();

        await handler.Handle(new LogoutCommand("jwt-id-1", _userId), CancellationToken.None);

        var stillRevoked = await LoadAsync(session.Id);
        stillRevoked.IsRevoked.Should().BeTrue();
    }

    // ── Scoping ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheJwtBelongsToAnotherUser_RevokesNothing()
    {
        var otherUsersSession = await AddSessionAsync(userId: Guid.NewGuid());
        var handler           = CreateHandler();

        // Same jti, but claimed by the wrong user.
        await handler.Handle(new LogoutCommand("jwt-id-1", _userId), CancellationToken.None);

        var untouched = await LoadAsync(otherUsersSession.Id);
        untouched.IsRevoked.Should().BeFalse(
            "a jti alone must not let one user terminate another's session");
    }

    [Fact]
    public async Task Handle_RevokesOnlyTheNamedSession()
    {
        var target      = await AddSessionAsync("jwt-id-1");
        var otherDevice = await AddSessionAsync("jwt-id-2");

        var handler = CreateHandler();

        await handler.Handle(new LogoutCommand("jwt-id-1", _userId), CancellationToken.None);

        (await LoadAsync(target.Id)).IsRevoked.Should().BeTrue();
        (await LoadAsync(otherDevice.Id)).IsRevoked.Should().BeFalse(
            "signing out on one device leaves the others signed in");
    }
}
