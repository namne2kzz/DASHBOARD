using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Sprints.Queries.ListSprints;
using DASHBOARD.Domain.Entities;
using DASHBOARD.UnitTests.Common;

namespace DASHBOARD.UnitTests.Application.Sprints.Queries;

/// <summary>Unit tests for <see cref="ListSprintsQueryHandler"/>.</summary>
public sealed class ListSprintsQueryHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public ListSprintsQueryHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private ListSprintsQueryHandler CreateHandler(IRequestUserContext user) => new(_db, user);

    private IRequestUserContext MemberUser() =>
        RequestUserContextMock.ForUser().AsMember(_repositoryId).Object;

    private async Task<Sprint> SeedSprintAsync(
        string name, DateOnly start, DateOnly end, Guid? repositoryId = null)
    {
        var sprint = new Sprint
        {
            RepositoryId = repositoryId ?? _repositoryId,
            Name         = name,
            StartDate    = start,
            EndDate      = end,
        };
        _db.Set<Sprint>().Add(sprint);
        await _db.SaveChangesAsync();
        return sprint;
    }

    // ── Access control ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserIsNotAMember_Throws()
    {
        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(new ListSprintsQuery(_repositoryId), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    // ── Scoping ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithNoSprints_ReturnsEmptyList()
    {
        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(new ListSprintsQuery(_repositoryId), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsOnlySprintsOfTheRequestedRepository()
    {
        await SeedSprintAsync("Mine",    new DateOnly(2026, 5, 4),  new DateOnly(2026, 5, 15));
        await SeedSprintAsync("Foreign", new DateOnly(2026, 6, 1),  new DateOnly(2026, 6, 12), Guid.NewGuid());

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(new ListSprintsQuery(_repositoryId), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Name.Should().Be("Mine");
    }

    // ── Ordering ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_OrdersByStartDateDescending()
    {
        await SeedSprintAsync("Oldest", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 14));
        await SeedSprintAsync("Newest", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 12));
        await SeedSprintAsync("Middle", new DateOnly(2026, 5, 4), new DateOnly(2026, 5, 15));

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(new ListSprintsQuery(_repositoryId), CancellationToken.None);

        result.Select(s => s.Name).Should().ContainInOrder("Newest", "Middle", "Oldest");
    }

    // ── Active flag ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_MarksOnlyTheSprintCoveringTodayAsActive()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedSprintAsync("Past",    today.AddDays(-30), today.AddDays(-16));
        await SeedSprintAsync("Current", today.AddDays(-1),  today.AddDays(1));
        await SeedSprintAsync("Future",  today.AddDays(10),  today.AddDays(20));

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(new ListSprintsQuery(_repositoryId), CancellationToken.None);

        result.Single(s => s.Name == "Current").IsActive.Should().BeTrue();
        result.Single(s => s.Name == "Past").IsActive.Should().BeFalse();
        result.Single(s => s.Name == "Future").IsActive.Should().BeFalse();
    }

    // ── HUB channel link (left join) ────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSprintHasNoChannelLink_ReturnsNullChannelFields()
    {
        await SeedSprintAsync("Unlinked", new DateOnly(2026, 5, 4), new DateOnly(2026, 5, 15));

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(new ListSprintsQuery(_repositoryId), CancellationToken.None);

        result.Should().ContainSingle("a sprint without a channel link must still be listed");
        result[0].HubChannelId.Should().BeNull();
        result[0].HubChannelUrl.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenSprintHasChannelLink_ReturnsChannelFields()
    {
        var sprint    = await SeedSprintAsync("Linked", new DateOnly(2026, 5, 4), new DateOnly(2026, 5, 15));
        var channelId = Guid.NewGuid();
        _db.Set<SprintChannelLink>().Add(new SprintChannelLink
        {
            SprintId      = sprint.Id,
            HubChannelId  = channelId,
            HubChannelUrl = $"https://hub.test/channels/{channelId}",
        });
        await _db.SaveChangesAsync();

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(new ListSprintsQuery(_repositoryId), CancellationToken.None);

        result[0].HubChannelId.Should().Be(channelId);
        result[0].HubChannelUrl.Should().Be($"https://hub.test/channels/{channelId}");
    }

    [Fact]
    public async Task Handle_WithMixedLinkedAndUnlinkedSprints_ReturnsAllOfThem()
    {
        var linked = await SeedSprintAsync("Linked",   new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 12));
        await SeedSprintAsync("Unlinked", new DateOnly(2026, 5, 4), new DateOnly(2026, 5, 15));
        _db.Set<SprintChannelLink>().Add(new SprintChannelLink
        {
            SprintId      = linked.Id,
            HubChannelId  = Guid.NewGuid(),
            HubChannelUrl = "https://hub.test/channels/x",
        });
        await _db.SaveChangesAsync();

        var handler = CreateHandler(MemberUser());

        var result = await handler.Handle(new ListSprintsQuery(_repositoryId), CancellationToken.None);

        result.Should().HaveCount(2, "the join must not drop sprints that have no channel link");
        result.Single(s => s.Name == "Linked").HubChannelId.Should().NotBeNull();
        result.Single(s => s.Name == "Unlinked").HubChannelId.Should().BeNull();
    }
}
