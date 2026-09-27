using System.Net;
using System.Net.Http.Json;
using DASHBOARD.IntegrationTests.Infrastructure;

namespace DASHBOARD.IntegrationTests.Authorization;

/// <summary>
/// Verifies the machine-to-machine <c>/internal/*</c> surface consumed by HUB.
/// </summary>
/// <remarks>
/// These routes deserve their own file because they are the one place in the system that does not
/// use the normal security model: the controller is <c>[AllowAnonymous]</c> and the only thing
/// standing in front of it is <c>InternalApiKeyMiddleware</c> checking a shared
/// <c>X-Internal-Token</c> header. There is no JWT, no tenant scoping and no privilege check, yet
/// the responses carry email addresses, organization membership and the full permission list of a
/// user. If that middleware ever stops running — reordered in <c>Program.cs</c>, dropped during a
/// refactor, or bypassed by a route that does not start with <c>/internal</c> — the endpoints
/// become a public dump of exactly the data an attacker would want, and nothing else in the test
/// suite would notice.
///
/// So the tests here are deliberately about the guard rather than the payload shape: every route is
/// probed once with no token and once with a wrong token, and only then with the right one.
/// </remarks>
[Collection(IntegrationTestCollection.Name)]
public sealed class InternalApiTests(SqlServerFixture database) : IAsyncLifetime
{
    private readonly ApiFactory _factory = new(database);

    private TenantSeed _tenant = null!;

    /// <summary>Seeds one tenant whose user and repository the internal routes can be asked about.</summary>
    public async Task InitializeAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        _tenant = await TestData.SeedTenantAsync(_factory, $"internal{suffix}", "INT");
    }

    /// <summary>Disposes the API host.</summary>
    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Creates a client that sends the token the middleware expects.</summary>
    private HttpClient TrustedCaller()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Internal-Token", ApiFactory.InternalToken);
        return client;
    }

    /// <summary>Every internal route, so the guard tests can sweep all of them.</summary>
    public static TheoryData<string> AllInternalRoutes =>
    [
        "internal/v1/users/00000000-0000-0000-0000-000000000001",
        "internal/v1/users?ids=00000000-0000-0000-0000-000000000001",
        "internal/v1/users/00000000-0000-0000-0000-000000000001/memberships",
        "internal/v1/users/00000000-0000-0000-0000-000000000001/settings",
        "internal/v1/repositories/00000000-0000-0000-0000-000000000001/members",
        "internal/v1/work-items/00000000-0000-0000-0000-000000000001",
    ];

    // ── The shared-token guard ──────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(AllInternalRoutes))]
    public async Task WithoutTheInternalToken_EveryRouteIsRejected(string route)
    {
        using var caller = _factory.CreateClient();

        var response = await caller.GetAsync(route);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the internal surface is anonymous by design and the shared token is its only guard");
    }

    [Theory]
    [MemberData(nameof(AllInternalRoutes))]
    public async Task WithAWrongInternalToken_EveryRouteIsRejected(string route)
    {
        using var caller = _factory.CreateClient();
        caller.DefaultRequestHeaders.Add("X-Internal-Token", "not-the-token");

        var response = await caller.GetAsync(route);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TheTokenComparisonIsCaseSensitive()
    {
        // The middleware compares with StringComparison.Ordinal. A case-insensitive comparison
        // would weaken a shared secret, so pin the current behaviour.
        using var caller = _factory.CreateClient();
        caller.DefaultRequestHeaders.Add("X-Internal-Token", ApiFactory.InternalToken.ToUpperInvariant());

        var response = await caller.GetAsync($"internal/v1/users/{_tenant.UserId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AJwtDoesNotSubstituteForTheInternalToken()
    {
        // A signed-in end user must not be able to reach the machine-to-machine surface just by
        // being authenticated — these two credentials are not interchangeable.
        using var signedIn = await ApiClient.SignedInAsync(_factory, _tenant);

        var response = await signedIn.GetAsync($"internal/v1/users/{_tenant.UserId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "a user bearer token is not an internal service credential");
    }

    // ── With the token, the routes answer ───────────────────────────────────

    [Fact]
    public async Task GetUser_ReturnsTheProfile()
    {
        using var caller = TrustedCaller();

        var response = await caller.GetAsync($"internal/v1/users/{_tenant.UserId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(_tenant.Email);
    }

    [Fact]
    public async Task GetUser_ForAnUnknownId_Is404()
    {
        using var caller = TrustedCaller();

        var response = await caller.GetAsync($"internal/v1/users/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUsers_ResolvesABatchAndSilentlyDropsUnknownIds()
    {
        using var caller = TrustedCaller();

        var response = await caller.GetAsync(
            $"internal/v1/users?ids={_tenant.UserId},{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var users = await response.Content.ReadFromJsonAsync<List<InternalUser>>();
        users.Should().ContainSingle("the unknown id is omitted rather than failing the call")
             .Which.Id.Should().Be(_tenant.UserId);
    }

    [Fact]
    public async Task GetUsers_WithAnUnparseableId_ReturnsAnEmptyListRatherThanFailing()
    {
        using var caller = TrustedCaller();

        var response = await caller.GetAsync("internal/v1/users?ids=not-a-guid");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var users = await response.Content.ReadFromJsonAsync<List<InternalUser>>();
        users.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMemberships_ReportsTheOrganizationAndTheRepositoryRole()
    {
        using var caller = TrustedCaller();

        var response = await caller.GetAsync($"internal/v1/users/{_tenant.UserId}/memberships");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var memberships = await response.Content.ReadFromJsonAsync<InternalMemberships>();
        memberships.Should().NotBeNull();
        memberships!.OrgId.Should().Be(_tenant.OrgId);
        memberships.OrgAlias.Should().Be(_tenant.OrgAlias);
        memberships.Repositories.Should().ContainSingle()
            .Which.RepositoryCode.Should().Be(_tenant.RepositoryCode);
    }

    [Fact]
    public async Task GetMemberships_CarriesThePermissionNamesHubRelieson()
    {
        // HUB gates its own UI on these strings. They come out of a JSON column and are projected
        // in memory (enum.ToString() has no SQL translation), so an empty list here would mean HUB
        // silently treats the user as having no rights at all.
        using var caller = TrustedCaller();

        var response = await caller.GetAsync($"internal/v1/users/{_tenant.UserId}/memberships");
        var memberships = await response.Content.ReadFromJsonAsync<InternalMemberships>();

        memberships!.Repositories.Single().Permissions
            .Should().NotBeEmpty("the seeded user holds a default role with real permissions");
    }

    [Fact]
    public async Task GetMemberships_ForAnUnknownUser_Is404()
    {
        using var caller = TrustedCaller();

        var response = await caller.GetAsync($"internal/v1/users/{Guid.NewGuid()}/memberships");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetRepositoryMembers_ListsTheSeededMember()
    {
        using var caller = TrustedCaller();

        var response = await caller.GetAsync(
            $"internal/v1/repositories/{_tenant.RepositoryId}/members");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var members = await response.Content.ReadFromJsonAsync<List<InternalUser>>();
        members.Should().ContainSingle().Which.Id.Should().Be(_tenant.UserId);
    }

    [Fact]
    public async Task GetUserSettings_ForAnUnknownUser_Is404RatherThanAnEmptyMap()
    {
        // An empty dictionary would look like "this user has no preferences" instead of "no such
        // user", which is the distinction the controller comment calls out.
        using var caller = TrustedCaller();

        var response = await caller.GetAsync($"internal/v1/users/{Guid.NewGuid()}/settings");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUserSettings_ForAKnownUserWithNoSettings_IsAnEmptyMap()
    {
        using var caller = TrustedCaller();

        var response = await caller.GetAsync($"internal/v1/users/{_tenant.UserId}/settings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var settings = await response.Content.ReadFromJsonAsync<Dictionary<string, string?>>();
        settings.Should().BeEmpty();
    }

    [Fact]
    public async Task GetWorkItem_ForAnUnknownId_Is404()
    {
        using var caller = TrustedCaller();

        var response = await caller.GetAsync($"internal/v1/work-items/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Response shapes, local to this file ─────────────────────────────────

    private sealed record InternalUser(Guid Id, string Name, string Email);

    private sealed record InternalMemberships(
        Guid                             UserId,
        bool                             IsGlobalAdmin,
        Guid                             OrgId,
        string                           OrgAlias,
        string                           OrgName,
        List<InternalRepositoryMembership> Repositories);

    private sealed record InternalRepositoryMembership(
        Guid         RepositoryId,
        string       RepositoryName,
        string       RepositoryCode,
        bool         IsArchived,
        string       RoleName,
        string       DefaultRole,
        List<string> Permissions);
}
