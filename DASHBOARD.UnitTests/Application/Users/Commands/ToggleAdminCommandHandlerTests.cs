using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Users.Commands.ToggleAdmin;
using DASHBOARD.Domain.Entities;
using DASHBOARD.UnitTests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.UnitTests.Application.Users.Commands;

/// <summary>
/// Unit tests for <see cref="ToggleAdminCommandHandler"/>.
/// </summary>
/// <remarks>
/// This handler was the subject of BUG-003 (Critical, privilege escalation): the permission check
/// read <c>!IsSelf(target) &amp;&amp; !IsGlobalAdmin</c>, so targeting your own id made the whole
/// condition false and skipped the admin check entirely — any signed-in user could grant themselves
/// global admin. The self-targeting tests below are the regression guard for that.
/// </remarks>
public sealed class ToggleAdminCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;

    /// <summary>Sets up an isolated in-memory database.</summary>
    public ToggleAdminCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private ToggleAdminCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private async Task<User> AddUserAsync(string name, bool isGlobalAdmin = false, Guid? id = null)
    {
        var user = new User
        {
            Name          = name,
            Email         = $"{name.ToLowerInvariant()}@test.local",
            PasswordHash  = "hash",
            PasswordSalt  = "salt",
            IsGlobalAdmin = isGlobalAdmin,
        };
        if (id.HasValue) user.WithId(id.Value);

        _db.Set<User>().Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    // ── BUG-003 regression: self-targeting must never bypass the admin check ──

    [Fact]
    public async Task Handle_WhenPlainUserTargetsThemselves_FailsAndDoesNotGrantAdmin()
    {
        var selfId = Guid.NewGuid();
        await AddUserAsync("Mallory", isGlobalAdmin: false, id: selfId);

        var handler = CreateHandler(RequestUserContextMock.ForUser(selfId).Object);

        var act = () => handler.Handle(new ToggleAdminCommand(selfId), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();

        var target = await _db.Set<User>().AsNoTracking().SingleAsync(u => u.Id == selfId);
        target.IsGlobalAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenGlobalAdminTargetsThemselves_Fails()
    {
        var selfId = Guid.NewGuid();
        await AddUserAsync("Admin", isGlobalAdmin: true, id: selfId);

        var handler = CreateHandler(RequestUserContextMock.ForUser(selfId).AsGlobalAdmin().Object);

        var act = () => handler.Handle(new ToggleAdminCommand(selfId), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>(
            "the rule requires another admin — self is excluded");

        var target = await _db.Set<User>().AsNoTracking().SingleAsync(u => u.Id == selfId);
        target.IsGlobalAdmin.Should().BeTrue("the admin must not be able to demote themselves");
    }

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenRequesterIsNotGlobalAdmin_FailsAndLeavesTargetUnchanged()
    {
        var target  = await AddUserAsync("Victim");
        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(new ToggleAdminCommand(target.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();

        var unchanged = await _db.Set<User>().AsNoTracking().SingleAsync(u => u.Id == target.Id);
        unchanged.IsGlobalAdmin.Should().BeFalse();
    }

    // ── Not found ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTargetDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(RequestUserContextMock.ForUser().AsGlobalAdmin().Object);

        var act = () => handler.Handle(new ToggleAdminCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenAdminPromotesAnotherUser_SetsGlobalAdmin()
    {
        var target  = await AddUserAsync("Promotable", isGlobalAdmin: false);
        var handler = CreateHandler(RequestUserContextMock.ForUser().AsGlobalAdmin().Object);

        var result = await handler.Handle(new ToggleAdminCommand(target.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = await _db.Set<User>().AsNoTracking().SingleAsync(u => u.Id == target.Id);
        updated.IsGlobalAdmin.Should().BeTrue();
        updated.UpdatedAt.Should().NotBeNull("Touch() must stamp the update time");
    }

    [Fact]
    public async Task Handle_WhenAdminDemotesAnotherAdmin_ClearsGlobalAdmin()
    {
        var target  = await AddUserAsync("Demotable", isGlobalAdmin: true);
        var handler = CreateHandler(RequestUserContextMock.ForUser().AsGlobalAdmin().Object);

        var result = await handler.Handle(new ToggleAdminCommand(target.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = await _db.Set<User>().AsNoTracking().SingleAsync(u => u.Id == target.Id);
        updated.IsGlobalAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_TogglingTwice_ReturnsToTheOriginalState()
    {
        var target  = await AddUserAsync("Flipper", isGlobalAdmin: false);
        var handler = CreateHandler(RequestUserContextMock.ForUser().AsGlobalAdmin().Object);

        await handler.Handle(new ToggleAdminCommand(target.Id), CancellationToken.None);
        await handler.Handle(new ToggleAdminCommand(target.Id), CancellationToken.None);

        var updated = await _db.Set<User>().AsNoTracking().SingleAsync(u => u.Id == target.Id);
        updated.IsGlobalAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DoesNotAffectOtherUsers()
    {
        var target     = await AddUserAsync("Target",    isGlobalAdmin: false);
        var bystander  = await AddUserAsync("Bystander", isGlobalAdmin: false);
        var handler    = CreateHandler(RequestUserContextMock.ForUser().AsGlobalAdmin().Object);

        await handler.Handle(new ToggleAdminCommand(target.Id), CancellationToken.None);

        var unaffected = await _db.Set<User>().AsNoTracking().SingleAsync(u => u.Id == bystander.Id);
        unaffected.IsGlobalAdmin.Should().BeFalse();
    }
}
