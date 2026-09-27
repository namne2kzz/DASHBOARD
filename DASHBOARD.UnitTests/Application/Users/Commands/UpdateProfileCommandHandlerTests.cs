using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Users.Commands.UpdateProfile;
using DASHBOARD.Domain.Entities;
using DASHBOARD.UnitTests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.UnitTests.Application.Users.Commands;

/// <summary>Unit tests for <see cref="UpdateProfileCommandHandler"/>.</summary>
/// <remarks>
/// The permission rule here is "self OR global admin", which reads as
/// <c>!IsSelf(target) &amp;&amp; !IsGlobalAdmin</c>. That shape is deliberate for this handler —
/// unlike <c>ToggleAdmin</c>, where the same shape was the BUG-003 privilege-escalation hole
/// because self-targeting must be forbidden there rather than allowed.
/// </remarks>
public sealed class UpdateProfileCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;

    /// <summary>Sets up an isolated in-memory database.</summary>
    public UpdateProfileCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private UpdateProfileCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private async Task<User> AddUserAsync(Guid? id = null)
    {
        var user = new User
        {
            Name         = "Original Name",
            Email        = "owner@test.local",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            AvatarClass  = "bg-slate-600",
        };
        if (id.HasValue) user.WithId(id.Value);

        _db.Set<User>().Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenRequesterIsNeitherOwnerNorAdmin_FailsAndLeavesProfileUnchanged()
    {
        var target  = await AddUserAsync();
        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(
            new UpdateProfileCommand(target.Id, "Hacked", "bg-red-600"), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();

        var unchanged = await _db.Set<User>().AsNoTracking().SingleAsync(u => u.Id == target.Id);
        unchanged.Name.Should().Be("Original Name");
        unchanged.AvatarClass.Should().Be("bg-slate-600");
    }

    [Fact]
    public async Task Handle_WhenRequesterIsTheOwner_Succeeds()
    {
        var selfId = Guid.NewGuid();
        await AddUserAsync(selfId);

        var handler = CreateHandler(RequestUserContextMock.ForUser(selfId).Object);

        var result = await handler.Handle(
            new UpdateProfileCommand(selfId, "My New Name", "bg-sky-600"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("a user may always edit their own profile");
    }

    [Fact]
    public async Task Handle_WhenRequesterIsGlobalAdmin_CanEditAnotherUsersProfile()
    {
        var target  = await AddUserAsync();
        var handler = CreateHandler(RequestUserContextMock.ForUser().AsGlobalAdmin().Object);

        var result = await handler.Handle(
            new UpdateProfileCommand(target.Id, "Renamed By Admin", "bg-sky-600"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = await _db.Set<User>().AsNoTracking().SingleAsync(u => u.Id == target.Id);
        updated.Name.Should().Be("Renamed By Admin");
    }

    // ── Not found ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(RequestUserContextMock.ForUser().AsGlobalAdmin().Object);

        var act = () => handler.Handle(
            new UpdateProfileCommand(Guid.NewGuid(), "Name", "bg-sky-600"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_UpdatesNameAvatarAndTimestamp()
    {
        var selfId = Guid.NewGuid();
        var target = await AddUserAsync(selfId);
        target.UpdatedAt.Should().BeNull();

        var handler = CreateHandler(RequestUserContextMock.ForUser(selfId).Object);

        var result = await handler.Handle(
            new UpdateProfileCommand(selfId, "Updated Name", "bg-emerald-600"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = await _db.Set<User>().AsNoTracking().SingleAsync(u => u.Id == selfId);
        updated.Name.Should().Be("Updated Name");
        updated.AvatarClass.Should().Be("bg-emerald-600");
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_DoesNotChangeTheEmailOrAdminFlag()
    {
        var selfId = Guid.NewGuid();
        await AddUserAsync(selfId);

        var handler = CreateHandler(RequestUserContextMock.ForUser(selfId).Object);

        await handler.Handle(
            new UpdateProfileCommand(selfId, "Updated Name", "bg-emerald-600"), CancellationToken.None);

        var updated = await _db.Set<User>().AsNoTracking().SingleAsync(u => u.Id == selfId);
        updated.Email.Should().Be("owner@test.local", "profile edits must not touch the login identity");
        updated.IsGlobalAdmin.Should().BeFalse("profile edits must not grant privileges");
    }
}
