using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Users.Commands.ToggleActive;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Tests.Application.Users.Commands;

/// <summary>Unit tests for <see cref="ToggleActiveCommandHandler"/>.</summary>
public sealed class ToggleActiveCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _requesterId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public ToggleActiveCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private ToggleActiveCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private IRequestUserContext AdminUser() =>
        RequestUserContextMock.ForUser(_requesterId).AsGlobalAdmin().Object;

    private async Task<User> AddUserAsync(string name, bool isDeleted = false, Guid? id = null)
    {
        var user = new User
        {
            Name         = name,
            Email        = $"{name.ToLowerInvariant()}@test.local",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            IsDeleted    = isDeleted,
            DeletedAt    = isDeleted ? DateTime.UtcNow.AddDays(-1) : null,
        };
        if (id.HasValue) user.WithId(id.Value);

        _db.Set<User>().Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    /// <summary>Reads a user bypassing the soft-delete filter, so deactivated rows stay visible.</summary>
    private Task<User> LoadAsync(Guid id) =>
        _db.Set<User>().IgnoreQueryFilters().AsNoTracking().SingleAsync(u => u.Id == id);

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenRequesterIsNotGlobalAdmin_FailsAndLeavesUserActive()
    {
        var target  = await AddUserAsync("Victim");
        var handler = CreateHandler(RequestUserContextMock.ForUser(_requesterId).Object);

        var result = await handler.Handle(new ToggleActiveCommand(target.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("global admins");
        (await LoadAsync(target.Id)).IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenAdminTargetsThemselves_Fails()
    {
        await AddUserAsync("Admin", id: _requesterId);
        var handler = CreateHandler(AdminUser());

        var result = await handler.Handle(new ToggleActiveCommand(_requesterId), CancellationToken.None);

        result.IsFailure.Should().BeTrue("an admin must not be able to deactivate their own account");
        (await LoadAsync(_requesterId)).IsDeleted.Should().BeFalse();
    }

    // ── Not found ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTargetDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(AdminUser());

        var act = () => handler.Handle(new ToggleActiveCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Deactivate ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserIsActive_DeactivatesAndStampsAudit()
    {
        var target  = await AddUserAsync("Active");
        var handler = CreateHandler(AdminUser());

        var result = await handler.Handle(new ToggleActiveCommand(target.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = await LoadAsync(target.Id);
        updated.IsDeleted.Should().BeTrue();
        updated.DeletedAt.Should().NotBeNull();
        updated.DeletedByUserId.Should().Be(_requesterId, "the acting admin is recorded");
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_AfterDeactivation_UserIsHiddenByTheSoftDeleteFilter()
    {
        var target  = await AddUserAsync("Active");
        var handler = CreateHandler(AdminUser());

        await handler.Handle(new ToggleActiveCommand(target.Id), CancellationToken.None);

        var visible = await _db.Set<User>().AsNoTracking().AnyAsync(u => u.Id == target.Id);
        visible.Should().BeFalse("deactivated users must drop out of ordinary queries");
    }

    // ── Reactivate ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserIsDeactivated_ReactivatesAndClearsAudit()
    {
        var target  = await AddUserAsync("Deactivated", isDeleted: true);
        var handler = CreateHandler(AdminUser());

        var result = await handler.Handle(new ToggleActiveCommand(target.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(
            "a deactivated user must still be reachable, which needs IgnoreQueryFilters");

        var updated = await LoadAsync(target.Id);
        updated.IsDeleted.Should().BeFalse();
        updated.DeletedAt.Should().BeNull();
        updated.DeletedByUserId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_AfterReactivation_UserIsVisibleAgain()
    {
        var target  = await AddUserAsync("Deactivated", isDeleted: true);
        var handler = CreateHandler(AdminUser());

        await handler.Handle(new ToggleActiveCommand(target.Id), CancellationToken.None);

        var visible = await _db.Set<User>().AsNoTracking().AnyAsync(u => u.Id == target.Id);
        visible.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_TogglingTwice_ReturnsToTheOriginalState()
    {
        var target  = await AddUserAsync("Flipper");
        var handler = CreateHandler(AdminUser());

        await handler.Handle(new ToggleActiveCommand(target.Id), CancellationToken.None);
        await handler.Handle(new ToggleActiveCommand(target.Id), CancellationToken.None);

        var updated = await LoadAsync(target.Id);
        updated.IsDeleted.Should().BeFalse();
        updated.DeletedAt.Should().BeNull();
    }
}
