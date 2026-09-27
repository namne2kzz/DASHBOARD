using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Users.Commands.ChangePassword;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DASHBOARD.Tests.Application.Users.Commands;

/// <summary>Unit tests for <see cref="ChangePasswordCommandHandler"/>.</summary>
public sealed class ChangePasswordCommandHandlerTests : IDisposable
{
    private const string OldHash = "old-hash";
    private const string OldSalt = "old-salt";
    private const string NewHash = "new-hash";
    private const string NewSalt = "new-salt";

    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Mock<IPasswordService>   _passwords = new();
    private readonly Guid                     _userId    = Guid.NewGuid();

    /// <summary>Sets up an isolated database and a password service that rejects by default.</summary>
    public ChangePasswordCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;

        _passwords.Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(false);
        _passwords.Setup(p => p.HashPassword(It.IsAny<string>()))
                  .Returns((NewHash, NewSalt));
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private ChangePasswordCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _passwords.Object, _database.Uow);

    /// <summary>Configures the password service to accept exactly this plaintext as the current password.</summary>
    private void AcceptCurrentPassword(string plaintext) =>
        _passwords.Setup(p => p.VerifyPassword(plaintext, OldHash, OldSalt)).Returns(true);

    private async Task<User> AddUserAsync(Guid id)
    {
        var user = new User
        {
            Name         = "Owner",
            Email        = "owner@test.local",
            PasswordHash = OldHash,
            PasswordSalt = OldSalt,
        }.WithId(id);

        _db.Set<User>().Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    // ── Ownership ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTargetIsAnotherUser_FailsAndLeavesPasswordUnchanged()
    {
        await AddUserAsync(_userId);
        AcceptCurrentPassword("Current1!");

        // Signed in as somebody else entirely.
        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var result = await handler.Handle(
            new ChangePasswordCommand(_userId, "Current1!", "Brand-New1!"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("your own password");

        var unchanged = await _db.Set<User>().AsNoTracking().SingleAsync(u => u.Id == _userId);
        unchanged.PasswordHash.Should().Be(OldHash);
    }

    [Fact]
    public async Task Handle_WhenGlobalAdminTargetsAnotherUser_StillFails()
    {
        await AddUserAsync(_userId);
        AcceptCurrentPassword("Current1!");

        var handler = CreateHandler(RequestUserContextMock.ForUser().AsGlobalAdmin().Object);

        var result = await handler.Handle(
            new ChangePasswordCommand(_userId, "Current1!", "Brand-New1!"), CancellationToken.None);

        result.IsFailure.Should().BeTrue("not even a global admin may change someone else's password");
    }

    // ── Old-password verification ───────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenOldPasswordIsWrong_FailsAndDoesNotRehash()
    {
        await AddUserAsync(_userId);
        AcceptCurrentPassword("Current1!");

        var handler = CreateHandler(RequestUserContextMock.ForUser(_userId).Object);

        var result = await handler.Handle(
            new ChangePasswordCommand(_userId, "WrongPassword1!", "Brand-New1!"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Current password is incorrect");

        _passwords.Verify(p => p.HashPassword(It.IsAny<string>()), Times.Never);

        var unchanged = await _db.Set<User>().AsNoTracking().SingleAsync(u => u.Id == _userId);
        unchanged.PasswordHash.Should().Be(OldHash);
        unchanged.PasswordSalt.Should().Be(OldSalt);
    }

    [Fact]
    public async Task Handle_VerifiesAgainstTheStoredHashAndSalt()
    {
        await AddUserAsync(_userId);
        AcceptCurrentPassword("Current1!");

        var handler = CreateHandler(RequestUserContextMock.ForUser(_userId).Object);

        await handler.Handle(
            new ChangePasswordCommand(_userId, "Current1!", "Brand-New1!"), CancellationToken.None);

        _passwords.Verify(p => p.VerifyPassword("Current1!", OldHash, OldSalt), Times.Once);
    }

    // ── Not found ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(RequestUserContextMock.ForUser(_userId).Object);

        var act = () => handler.Handle(
            new ChangePasswordCommand(_userId, "Current1!", "Brand-New1!"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithCorrectOldPassword_StoresNewHashAndSalt()
    {
        await AddUserAsync(_userId);
        AcceptCurrentPassword("Current1!");

        var handler = CreateHandler(RequestUserContextMock.ForUser(_userId).Object);

        var result = await handler.Handle(
            new ChangePasswordCommand(_userId, "Current1!", "Brand-New1!"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _passwords.Verify(p => p.HashPassword("Brand-New1!"), Times.Once);

        var updated = await _db.Set<User>().AsNoTracking().SingleAsync(u => u.Id == _userId);
        updated.PasswordHash.Should().Be(NewHash);
        updated.PasswordSalt.Should().Be(NewSalt, "the salt must be rotated alongside the hash");
        updated.UpdatedAt.Should().NotBeNull();
    }
}
