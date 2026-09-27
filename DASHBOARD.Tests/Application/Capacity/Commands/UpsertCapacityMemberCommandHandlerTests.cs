using DASHBOARD.Application.Capacity.Commands.UpsertCapacityMember;
using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DASHBOARD.Tests.Application.Capacity.Commands;

/// <summary>Unit tests for <see cref="UpsertCapacityMemberCommandHandler"/>.</summary>
public sealed class UpsertCapacityMemberCommandHandlerTests : IDisposable
{
    private readonly TestDatabase              _database;
    private readonly TestApplicationDbContext  _db;
    private readonly Mock<IHubChannelService>  _hub = new();
    private readonly Guid                      _repositoryId = Guid.NewGuid();
    private readonly Guid                      _sprintId     = Guid.NewGuid();

    /// <summary>Sets up an isolated database holding one sprint.</summary>
    public UpsertCapacityMemberCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;

        _db.Set<Sprint>().Add(new Sprint
        {
            RepositoryId = _repositoryId,
            Name         = "Sprint 1",
            StartDate    = new DateOnly(2026, 5, 4),
            EndDate      = new DateOnly(2026, 5, 15),
        }.WithId(_sprintId));
        _db.SaveChanges();
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private UpsertCapacityMemberCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow, _hub.Object);

    private IRequestUserContext AuthorizedUser() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.ManageCapacity, _repositoryId)
            .AsMember(_repositoryId).Object;

    private async Task<User> AddUserAsync(string name = "Alice")
    {
        var user = new User
        {
            Name         = name,
            Email        = $"{name.ToLowerInvariant()}@test.local",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            AvatarClass  = "bg-sky-600",
        };
        _db.Set<User>().Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private UpsertCapacityMemberCommand Command(
        Guid    userId,
        string  role       = "Developer",
        decimal hoursPerDay = 8m,
        decimal overtime    = 0m,
        Guid?   sprintId    = null) =>
        new(_repositoryId, sprintId ?? _sprintId, userId, role, hoursPerDay, overtime);

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutManageCapacityPrivilege_Throws()
    {
        var alice   = await AddUserAsync();
        var handler = CreateHandler(RequestUserContextMock.ForUser().AsMember(_repositoryId).Object);

        var act = () => handler.Handle(Command(alice.Id), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        (await _db.Set<CapacityMember>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheCallerIsNotARepositoryMember_Throws()
    {
        // Note: this guard inspects the *caller's* membership, not the membership of the user
        // being given a capacity row. A caller holding ManageCapacity without being a member is
        // the only way to reach it.
        var alice   = await AddUserAsync();
        var handler = CreateHandler(
            RequestUserContextMock.ForUser()
                .WithPrivilege(SystemFunction.ManageCapacity, _repositoryId).Object);

        var act = () => handler.Handle(Command(alice.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Sprint and user resolution ──────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSprintDoesNotExist_ThrowsNotFound()
    {
        var alice   = await AddUserAsync();
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            Command(alice.Id, sprintId: Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenTheUserDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(Command(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Insert ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ForANewMember_CreatesTheCapacityRow()
    {
        var alice   = await AddUserAsync();
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            Command(alice.Id, "Scrum Master", hoursPerDay: 6m, overtime: 2m),
            CancellationToken.None);

        result.UserId.Should().Be(alice.Id);
        result.UserName.Should().Be("Alice");
        result.UserAvatar.Should().Be("bg-sky-600");
        result.Role.Should().Be("Scrum Master");
        result.HoursPerDay.Should().Be(6m);
        result.OvertimeHoursPerDay.Should().Be(2m);

        var persisted = await _db.Set<CapacityMember>().AsNoTracking().SingleAsync();
        persisted.SprintId.Should().Be(_sprintId);
        persisted.RepositoryId.Should().Be(_repositoryId);
    }

    // ── Update (idempotent upsert) ──────────────────────────────────────────

    [Fact]
    public async Task Handle_ForAnExistingMember_UpdatesInsteadOfDuplicating()
    {
        var alice   = await AddUserAsync();
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(Command(alice.Id, hoursPerDay: 8m), CancellationToken.None);
        var second = await handler.Handle(
            Command(alice.Id, "Tester", hoursPerDay: 4m, overtime: 1m), CancellationToken.None);

        (await _db.Set<CapacityMember>().CountAsync()).Should().Be(1, "the upsert is by sprint + user");

        var updated = await _db.Set<CapacityMember>().AsNoTracking().SingleAsync();
        updated.Id.Should().Be(second.Id, "the same row is reused");
        updated.Role.Should().Be("Tester");
        updated.HoursPerDay.Should().Be(4m);
        updated.OvertimeHoursPerDay.Should().Be(1m);
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_AllowsTheSameUserInTwoSprints()
    {
        var secondSprintId = Guid.NewGuid();
        _db.Set<Sprint>().Add(new Sprint
        {
            RepositoryId = _repositoryId,
            Name         = "Sprint 2",
            StartDate    = new DateOnly(2026, 6, 1),
            EndDate      = new DateOnly(2026, 6, 12),
        }.WithId(secondSprintId));
        await _db.SaveChangesAsync();

        var alice   = await AddUserAsync();
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(Command(alice.Id), CancellationToken.None);
        await handler.Handle(Command(alice.Id, sprintId: secondSprintId), CancellationToken.None);

        (await _db.Set<CapacityMember>().CountAsync()).Should()
            .Be(2, "capacity is configured per sprint");
    }

    // ── HUB channel sync (fire-and-forget) ──────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheSprintHasNoChannel_DoesNotCallHub()
    {
        var alice   = await AddUserAsync();
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(Command(alice.Id), CancellationToken.None);

        _hub.Verify(h => h.AddMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTheSprintHasAChannel_AddsTheMemberToIt()
    {
        var channelId = Guid.NewGuid();
        _db.Set<SprintChannelLink>().Add(new SprintChannelLink
        {
            SprintId      = _sprintId,
            HubChannelId  = channelId,
            HubChannelUrl = $"https://hub.test/channels/{channelId}",
        });
        await _db.SaveChangesAsync();

        var alice   = await AddUserAsync();
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(Command(alice.Id), CancellationToken.None);

        _hub.Verify(h => h.AddMemberAsync(channelId, alice.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTheHubCallFaultsAsynchronously_StillReturnsTheCapacityRow()
    {
        var channelId = await AddChannelLinkAsync();

        // A faulted Task, which is what an HTTP failure inside the HUB client produces. The handler
        // discards the task with `_ =`, so the fault is never observed here.
        _hub.Setup(h => h.AddMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("HUB is down"));

        var alice   = await AddUserAsync();
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(Command(alice.Id), CancellationToken.None);

        result.UserId.Should().Be(alice.Id,
            "the HUB sync is fire-and-forget and must never fail the capacity change");
        (await _db.Set<CapacityMember>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenTheHubCallThrowsSynchronously_StillReturnsTheCapacityRow()
    {
        await AddChannelLinkAsync();

        // Throwing before returning a Task — e.g. an argument check or a disposed HttpClient —
        // is not covered by the faulted-task case above, because `_ =` cannot swallow it.
        _hub.Setup(h => h.AddMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Throws(new ObjectDisposedException("HttpClient"));

        var alice   = await AddUserAsync();
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(Command(alice.Id), CancellationToken.None);

        await act.Should().NotThrowAsync(
            "capacity is already committed at this point — a HUB client fault must not surface");
        (await _db.Set<CapacityMember>().CountAsync()).Should().Be(1);
    }

    private async Task<Guid> AddChannelLinkAsync()
    {
        var channelId = Guid.NewGuid();
        _db.Set<SprintChannelLink>().Add(new SprintChannelLink
        {
            SprintId      = _sprintId,
            HubChannelId  = channelId,
            HubChannelUrl = $"https://hub.test/channels/{channelId}",
        });
        await _db.SaveChangesAsync();
        return channelId;
    }
}
