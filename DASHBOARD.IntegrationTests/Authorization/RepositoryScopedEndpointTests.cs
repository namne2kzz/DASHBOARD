using System.Net;
using DASHBOARD.Domain.Constants;
using DASHBOARD.IntegrationTests.Infrastructure;

namespace DASHBOARD.IntegrationTests.Authorization;

/// <summary>
/// Sweeps every read endpoint that hangs off <c>repositories/{repoId}</c> and checks the two
/// properties each of them must have: it is closed to anonymous callers, and it is closed to a
/// signed-in user from another organization.
/// </summary>
/// <remarks>
/// <c>EndpointAuthorizationTests</c> and <c>CrossTenantAccessTests</c> each prove these rules
/// thoroughly, but only on a handful of routes. The risk this file addresses is different: a new
/// controller gets added without <c>[Authorize]</c>, or with a query that filters by
/// <c>repoId</c> but never checks that the caller belongs to that repository. Nothing about such a
/// controller looks wrong in review, and no existing test would fail.
///
/// So the coverage here is deliberately wide and shallow — one probe per route rather than a full
/// behavioural suite. The theory data doubles as an inventory of the repository-scoped surface, and
/// a route added without a corresponding entry is the gap this file is meant to make visible.
///
/// Write endpoints are left to the two files named above: a POST or PUT needs a valid body before
/// it reaches the authorisation check, so a sweep of them would assert mostly on model binding.
/// </remarks>
[Collection(IntegrationTestCollection.Name)]
public sealed class RepositoryScopedEndpointTests(SqlServerFixture database) : IAsyncLifetime
{
    private readonly ApiFactory _factory = new(database);

    private TenantSeed _owner    = null!;
    private TenantSeed _outsider = null!;
    private HttpClient _ownerClient    = null!;
    private HttpClient _outsiderClient = null!;

    /// <summary>Seeds the repository under test plus an unrelated tenant to probe it with.</summary>
    public async Task InitializeAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        _owner    = await TestData.SeedTenantAsync(_factory, $"owner{suffix}",  "OWN");
        _outsider = await TestData.SeedTenantAsync(_factory, $"other{suffix}", "OTH");

        _ownerClient    = await ApiClient.SignedInAsync(_factory, _owner);
        _outsiderClient = await ApiClient.SignedInAsync(_factory, _outsider);
    }

    /// <summary>Disposes both clients and the API host.</summary>
    public Task DisposeAsync()
    {
        _ownerClient.Dispose();
        _outsiderClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Every GET route under a repository, as a path fragment appended to
    /// <c>repositories/{repoId}</c>. Ids inside a fragment are placeholders: the request is refused
    /// before the row is ever looked up, which is exactly the property under test.
    /// </summary>
    public static TheoryData<string> RepositoryScopedReads =>
    [
        "overview",
        "search?q=anything",
        "audit-log",
        "members",
        "sprints",
        "backlog",
        "roles",
        "items",
        "items/picker",
        "board/columns",
        "invitations",
        "metadata",
        "git-repositories",
        "git-repositories/overview",
    ];

    private static string Route(Guid repositoryId, string fragment) =>
        $"{ApiClient.V1}/repositories/{repositoryId}/{fragment}";

    // ── Closed to anonymous callers ─────────────────────────────────────────

    [Theory]
    [MemberData(nameof(RepositoryScopedReads))]
    public async Task WithoutAToken_TheEndpointIs401(string fragment)
    {
        using var anonymous = ApiClient.Anonymous(_factory);

        var response = await anonymous.GetAsync(Route(_owner.RepositoryId, fragment));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "no repository-scoped route is public");
    }

    // ── Closed to another organization ──────────────────────────────────────

    [Theory]
    [MemberData(nameof(RepositoryScopedReads))]
    public async Task ForAnotherOrganizationsRepository_TheEndpointRefuses(string fragment)
    {
        var response = await _outsiderClient.GetAsync(Route(_owner.RepositoryId, fragment));

        response.StatusCode.Should().BeOneOf(
            [HttpStatusCode.Forbidden, HttpStatusCode.NotFound],
            "a signed-in outsider is authenticated, so the answer is 403 — or 404 where the route " +
            "declines to confirm the repository exists at all");
    }

    [Theory]
    [MemberData(nameof(RepositoryScopedReads))]
    public async Task ForAnotherOrganizationsRepository_NothingLeaksIntoTheBody(string fragment)
    {
        // A refusal status with the data attached would still be a leak. The outsider's own
        // repository code must never appear either — that would mean the route quietly fell back
        // to the caller's own tenant instead of refusing.
        var response = await _outsiderClient.GetAsync(Route(_owner.RepositoryId, fragment));
        var body     = await response.Content.ReadAsStringAsync();

        body.Should().NotContain(_owner.RepositoryCode);
        body.Should().NotContain(_owner.Email);
    }

    // ── Open to the repository's own member ─────────────────────────────────

    [Theory]
    [MemberData(nameof(RepositoryScopedReads))]
    public async Task ForTheirOwnRepository_AScrumMasterIsNotRefused(string fragment)
    {
        // The mirror image of the tests above: if a route refused everyone, the two sweeps would
        // pass while the endpoint was simply broken. The seeded user is a Scrum Master, which
        // carries the read privileges across this surface.
        var response = await _ownerClient.GetAsync(Route(_owner.RepositoryId, fragment));

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    // ── A route that skips the version segment ──────────────────────────────

    [Fact]
    public async Task TheUnversionedByKeyRouteStillRequiresAToken()
    {
        // SprintTaskDetailController declares this one with a leading slash and no
        // api/v{version} prefix, so it does not inherit the versioned route template. A route
        // that sits outside the usual shape is exactly the kind that gets missed.
        using var anonymous = ApiClient.Anonymous(_factory);

        var response = await anonymous.GetAsync(
            $"api/repositories/{_owner.RepositoryId}/sprint-tasks/by-key/{_owner.RepositoryCode}-1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "skipping the version segment must not skip authentication");
    }

    [Fact]
    public async Task TheUnversionedByKeyRouteIsClosedToAnotherOrganization()
    {
        var response = await _outsiderClient.GetAsync(
            $"api/repositories/{_owner.RepositoryId}/sprint-tasks/by-key/{_owner.RepositoryCode}-1");

        response.StatusCode.Should().BeOneOf([HttpStatusCode.Forbidden, HttpStatusCode.NotFound]);
    }

    // ── A route that carries no repository data ─────────────────────────────

    [Fact]
    public async Task TheMetadataKeyCatalogueNeedsAuthenticationButNotMembership()
    {
        // metadata/keys sits under repositories/{repoId} but ignores it entirely: it returns the
        // MetadataKey enum, which is the same for every tenant and holds no customer data. So it
        // is kept out of the cross-tenant sweep above — an outsider gets 200, and that is correct
        // rather than a leak. It still must not be reachable anonymously.
        using var anonymous = ApiClient.Anonymous(_factory);

        var withoutToken = await anonymous.GetAsync(Route(_owner.RepositoryId, "metadata/keys"));
        var asOutsider   = await _outsiderClient.GetAsync(Route(_owner.RepositoryId, "metadata/keys"));

        withoutToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        asOutsider.StatusCode.Should().Be(HttpStatusCode.OK,
            "the catalogue is a static enum, identical for every organization");

        var body = await asOutsider.Content.ReadAsStringAsync();
        body.Should().NotContain(_owner.RepositoryCode,
            "even so, nothing about the named repository may appear");
    }

    // ── Privilege, not just membership ──────────────────────────────────────

    [Fact]
    public async Task ADeveloperCannotReadTheAuditLog()
    {
        // Membership alone must not open every read. The audit log is the clearest case: a
        // developer is a legitimate member of the repository but has no business reading it.
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var developer = await TestData.SeedTenantAsync(
            _factory, $"dev{suffix}", "DEV", roleName: DefaultRoleDefinitions.Developer);

        using var developerClient = await ApiClient.SignedInAsync(_factory, developer);

        var response = await developerClient.GetAsync(Route(developer.RepositoryId, "audit-log"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "reading the audit log needs a privilege the Developer role does not carry");
    }
}
