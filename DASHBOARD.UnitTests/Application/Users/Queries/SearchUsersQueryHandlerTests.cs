using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Users.Queries.SearchUsers;
using DASHBOARD.Domain.Entities;
using DASHBOARD.UnitTests.Common;

namespace DASHBOARD.UnitTests.Application.Users.Queries;

/// <summary>Unit tests for <see cref="SearchUsersQueryHandler"/>.</summary>
public sealed class SearchUsersQueryHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _orgId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public SearchUsersQueryHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private SearchUsersQueryHandler CreateHandler() =>
        new(_db, RequestUserContextMock.ForUser(orgId: _orgId).Object);

    private async Task<User> AddUserAsync(
        string name, string? email = null, Guid? orgId = null, bool isDeleted = false)
    {
        var user = new User
        {
            OrgId        = orgId ?? _orgId,
            Name         = name,
            Email        = email ?? $"{name.ToLowerInvariant()}@test.local",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            IsDeleted    = isDeleted,
            DeletedAt    = isDeleted ? DateTime.UtcNow : null,
        };
        _db.Set<User>().Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task AddMembershipAsync(Guid repositoryId, Guid userId)
    {
        _db.Set<RepositoryMember>().Add(new RepositoryMember
        {
            RepositoryId = repositoryId,
            UserId       = userId,
            RoleId       = Guid.NewGuid(),
            DefaultRole  = "Developer",
        });
        await _db.SaveChangesAsync();
    }

    // ── Matching ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_MatchesOnNameFragment()
    {
        await AddUserAsync("Alexandra");
        await AddUserAsync("Bob");

        var result = await CreateHandler().Handle(new SearchUsersQuery("lexan"), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Name.Should().Be("Alexandra");
    }

    [Fact]
    public async Task Handle_MatchesOnEmailFragment()
    {
        await AddUserAsync("Alice", email: "a.smith@corp.local");
        await AddUserAsync("Bob",   email: "bob@test.local");

        var result = await CreateHandler().Handle(new SearchUsersQuery("smith"), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Email.Should().Be("a.smith@corp.local");
    }

    [Theory]
    [InlineData("ALICE")]
    [InlineData("alice")]
    [InlineData("AlIcE")]
    public async Task Handle_MatchingIsCaseInsensitive(string term)
    {
        await AddUserAsync("Alice");

        var result = await CreateHandler().Handle(new SearchUsersQuery(term), CancellationToken.None);

        result.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_TrimsTheSearchTerm()
    {
        await AddUserAsync("Alice");

        var result = await CreateHandler().Handle(new SearchUsersQuery("  alice  "), CancellationToken.None);

        result.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_WhenNothingMatches_ReturnsEmptyList()
    {
        await AddUserAsync("Alice");

        var result = await CreateHandler().Handle(new SearchUsersQuery("zzzz"), CancellationToken.None);

        result.Should().BeEmpty();
    }

    // ── Organization scoping ────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ExcludesUsersFromOtherOrganizations()
    {
        await AddUserAsync("Alice");
        await AddUserAsync("Alicia", orgId: Guid.NewGuid());

        var result = await CreateHandler().Handle(new SearchUsersQuery("ali"), CancellationToken.None);

        result.Should().ContainSingle("search must never leak users across tenants");
        result[0].Name.Should().Be("Alice");
    }

    // ── Active-only ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ExcludesDeactivatedUsers()
    {
        await AddUserAsync("ActiveAlice");
        await AddUserAsync("GoneAlice", isDeleted: true);

        var result = await CreateHandler().Handle(new SearchUsersQuery("alice"), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Name.Should().Be("ActiveAlice");
    }

    // ── Repository exclusion ────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutExcludeRepoId_ReturnsEveryMatch()
    {
        var repoId = Guid.NewGuid();
        var alice  = await AddUserAsync("Alice");
        await AddMembershipAsync(repoId, alice.Id);

        var result = await CreateHandler().Handle(new SearchUsersQuery("alice"), CancellationToken.None);

        result.Should().ContainSingle("no repository filter was requested");
    }

    [Fact]
    public async Task Handle_WithExcludeRepoId_OmitsExistingMembers()
    {
        var repoId = Guid.NewGuid();
        var member = await AddUserAsync("AliceMember");
        await AddUserAsync("AliceOutsider");
        await AddMembershipAsync(repoId, member.Id);

        var result = await CreateHandler().Handle(
            new SearchUsersQuery("alice", repoId), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Name.Should().Be("AliceOutsider");
    }

    [Fact]
    public async Task Handle_ExclusionIsScopedToTheNamedRepository()
    {
        var targetRepo = Guid.NewGuid();
        var otherRepo  = Guid.NewGuid();
        var alice      = await AddUserAsync("Alice");
        await AddMembershipAsync(otherRepo, alice.Id);

        var result = await CreateHandler().Handle(
            new SearchUsersQuery("alice", targetRepo), CancellationToken.None);

        result.Should().ContainSingle(
            "membership in an unrelated repository must not exclude the user");
    }

    // ── Ordering and limit ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_OrdersResultsByName()
    {
        await AddUserAsync("TestZach");
        await AddUserAsync("TestAlice");
        await AddUserAsync("TestMary");

        var result = await CreateHandler().Handle(new SearchUsersQuery("test"), CancellationToken.None);

        result.Select(u => u.Name).Should().ContainInOrder("TestAlice", "TestMary", "TestZach");
    }

    [Fact]
    public async Task Handle_CapsResultsAtTwenty()
    {
        for (var i = 0; i < 25; i++)
            await AddUserAsync($"Tester{i:D2}");

        var result = await CreateHandler().Handle(new SearchUsersQuery("tester"), CancellationToken.None);

        result.Should().HaveCount(20, "the picker takes at most 20 candidates");
        result[0].Name.Should().Be("Tester00", "the cap applies after ordering by name");
    }
}
