using DASHBOARD.Application.Capacity.Commands.RemoveCapacityMember;
using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.UnitTests.Common;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DASHBOARD.UnitTests.Application.Capacity.Commands;

/// <summary>Unit tests for <see cref="RemoveCapacityMemberCommandHandler"/>.</summary>
public sealed class RemoveCapacityMemberCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Mock<IHubChannelService> _hub = new();
    private readonly Guid                     _repositoryId = Guid.NewGuid();
    private readonly Guid                     _sprintId     = Guid.NewGuid();

    /// <summary>Sets up an isolated database holding one sprint.</summary>
    public RemoveCapacityMemberCommandHandlerTests()
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

    private RemoveCapacityMemberCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow, _hub.Object);

    private IRequestUserContext AuthorizedUser() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.ManageCapacity, _repositoryId).Object;

    private async Task<CapacityMember> AddCapacityAsync(Guid? sprintId = null)
    {
        var member = new CapacityMember
        {
            SprintId     = sprintId ?? _sprintId,
            RepositoryId = _repositoryId,
            UserId       = Guid.NewGuid(),
            Role         = "Developer",
            HoursPerDay  = 8m,
        };
        _db.Set<CapacityMember>().Add(member);
        await _db.SaveChangesAsync();
        return member;
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

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutManageCapacityPrivilege_FailsAndKeepsTheRow()
    {
        var member  = await AddCapacityAsync();
        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(
            new RemoveCapacityMemberCommand(_repositoryId, _sprintId, member.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await _db.Set<CapacityMember>().CountAsync()).Should().Be(1);
    }

    // ── Not found ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheRowDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new RemoveCapacityMemberCommand(_repositoryId, _sprintId, Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenTheRowBelongsToAnotherSprint_ThrowsNotFound()
    {
        var otherSprintId = Guid.NewGuid();
        var member        = await AddCapacityAsync(sprintId: otherSprintId);
        var handler       = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new RemoveCapacityMemberCommand(_repositoryId, _sprintId, member.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Removal ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_RemovesTheCapacityRow()
    {
        var member  = await AddCapacityAsync();
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new RemoveCapacityMemberCommand(_repositoryId, _sprintId, member.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _db.Set<CapacityMember>().AnyAsync(c => c.Id == member.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_LeavesOtherMembersOfTheSprintAlone()
    {
        var target    = await AddCapacityAsync();
        var bystander = await AddCapacityAsync();
        var handler   = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new RemoveCapacityMemberCommand(_repositoryId, _sprintId, target.Id),
            CancellationToken.None);

        (await _db.Set<CapacityMember>().AnyAsync(c => c.Id == bystander.Id)).Should().BeTrue();
    }

    // ── HUB channel sync (fire-and-forget) ──────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheSprintHasNoChannel_DoesNotCallHub()
    {
        var member  = await AddCapacityAsync();
        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new RemoveCapacityMemberCommand(_repositoryId, _sprintId, member.Id),
            CancellationToken.None);

        _hub.Verify(h => h.RemoveMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTheSprintHasAChannel_RemovesTheMemberFromIt()
    {
        var channelId = await AddChannelLinkAsync();
        var member    = await AddCapacityAsync();
        var handler   = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new RemoveCapacityMemberCommand(_repositoryId, _sprintId, member.Id),
            CancellationToken.None);

        _hub.Verify(h => h.RemoveMemberAsync(channelId, member.UserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTheHubCallFaultsAsynchronously_StillSucceeds()
    {
        await AddChannelLinkAsync();
        _hub.Setup(h => h.RemoveMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("HUB is down"));

        var member  = await AddCapacityAsync();
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new RemoveCapacityMemberCommand(_repositoryId, _sprintId, member.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _db.Set<CapacityMember>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheHubCallThrowsSynchronously_StillSucceeds()
    {
        // A client that throws before returning a task is not covered by discarding the task —
        // the removal is already committed, so this must not reach the caller.
        await AddChannelLinkAsync();
        _hub.Setup(h => h.RemoveMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Throws(new ObjectDisposedException("HttpClient"));

        var member  = await AddCapacityAsync();
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new RemoveCapacityMemberCommand(_repositoryId, _sprintId, member.Id),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
        (await _db.Set<CapacityMember>().CountAsync()).Should().Be(0);
    }
}
