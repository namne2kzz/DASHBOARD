using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DASHBOARD.Domain.Constants;
using DASHBOARD.Domain.Enums;
using DASHBOARD.IntegrationTests.Infrastructure;

namespace DASHBOARD.IntegrationTests.Authorization;

/// <summary>
/// Checks the authorisation layers that sit in front of the handlers.
/// </summary>
/// <remarks>
/// Unit tests prove a handler refuses a caller without the right privilege. They cannot prove the
/// request ever reaches that check: an <c>[Authorize]</c> attribute left off a controller, a route
/// registered outside the authenticated pipeline, or a middleware ordering mistake all bypass the
/// handler entirely. BUG-022 was precisely this shape — the handler was missing its privilege check
/// and the controller only carried a class-level <c>[Authorize]</c>, so two layers were open at once.
/// </remarks>
[Collection(IntegrationTestCollection.Name)]
public sealed class EndpointAuthorizationTests(SqlServerFixture database) : IAsyncLifetime
{
    private readonly ApiFactory _factory = new(database);

    private TenantSeed _scrumMaster = null!;
    private TenantSeed _developer   = null!;
    private HttpClient _developerClient = null!;

    /// <summary>
    /// Seeds two tenants: one whose user holds Scrum Master, one whose user is only a Developer.
    /// The Developer is the interesting caller — authenticated, a genuine member, but short of the
    /// management privileges.
    /// </summary>
    public async Task InitializeAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        _scrumMaster = await TestData.SeedTenantAsync(
            _factory, $"sm{suffix}", "SMR", roleName: DefaultRoleDefinitions.ScrumMaster);

        _developer = await TestData.SeedTenantAsync(
            _factory, $"dev{suffix}", "DEV", roleName: DefaultRoleDefinitions.Developer);

        _developerClient = await ApiClient.SignedInAsync(_factory, _developer);
    }

    /// <summary>Disposes the client and the API host.</summary>
    public Task DisposeAsync()
    {
        _developerClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // ── Unauthenticated requests ────────────────────────────────────────────

    [Theory]
    [InlineData("repositories")]
    [InlineData("my-work")]
    [InlineData("users")]
    public async Task ProtectedEndpointsRejectAnonymousRequests(string path)
    {
        using var anonymous = ApiClient.Anonymous(_factory);

        var response = await anonymous.GetAsync($"{ApiClient.V1}/{path}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AGarbageBearerTokenIsRejected()
    {
        using var client = ApiClient.Anonymous(_factory);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "not.a.real.token");

        var response = await client.GetAsync($"{ApiClient.V1}/my-work");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ATokenSignedWithTheWrongKeyIsRejected()
    {
        // A JWT that is well formed but signed by somebody else must not be accepted — this is the
        // check that a misconfigured IssuerSigningKey would silently remove.
        const string foreignToken =
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9" +
            ".eyJ1aWQiOiJmMDBkZjAwZC0wMDAwLTAwMDAtMDAwMC0wMDAwMDAwMDAwMDAiLCJleHAiOjk5OTk5OTk5OTl9" +
            ".ZmFrZS1zaWduYXR1cmUtbm90LXZhbGlk";

        using var client = ApiClient.Anonymous(_factory);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", foreignToken);

        var response = await client.GetAsync($"{ApiClient.V1}/my-work");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Public endpoints stay public ────────────────────────────────────────

    [Fact]
    public async Task LoginIsReachableWithoutAToken()
    {
        using var anonymous = ApiClient.Anonymous(_factory);

        var response = await anonymous.PostAsJsonAsync(
            $"{ApiClient.V1}/auth/login",
            new { orgAlias = "nosuchorg", email = "nobody@test.local", password = "whatever" });

        // The endpoint itself answered rather than the bearer middleware rejecting the request,
        // which is what a missing [AllowAnonymous] would look like. Login reports bad credentials
        // as 401 too, so the two are told apart by the body: the handler returns an error message,
        // the middleware returns nothing at all.
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("error",
            "the response came from the login handler, not from the authentication middleware");
    }

    // ── Authenticated but under-privileged ──────────────────────────────────

    [Fact]
    public async Task ADeveloperCannotCreateASprint()
    {
        var response = await _developerClient.PostAsJsonAsync(
            $"{ApiClient.V1}/repositories/{_developer.RepositoryId}/sprints",
            new { name = "Unauthorised sprint", startDate = "2026-07-01", endDate = "2026-07-14" });

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "managing sprints needs ManageSprint, which the Developer role does not carry");

        await using var db = database.CreateContext();
        db.Sprints.Any(s => s.Name == "Unauthorised sprint").Should().BeFalse();
    }

    [Fact]
    public async Task ADeveloperCannotCreateARole()
    {
        var response = await _developerClient.PostAsJsonAsync(
            $"{ApiClient.V1}/repositories/{_developer.RepositoryId}/roles",
            new
            {
                name             = "Unauthorised role",
                description      = "Should not exist",
                permissions      = new[] { (int)SystemFunction.ViewRepository },
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "creating roles needs ManageRoles, which the Developer role does not carry");

        await using var db = database.CreateContext();
        db.Roles.Any(r => r.Name == "Unauthorised role").Should().BeFalse();
    }

    [Fact]
    public async Task ADeveloperCannotAddAMember()
    {
        await using var db = database.CreateContext();
        var roleId = db.Roles.First(r => r.RepositoryId == _developer.RepositoryId).Id;

        var response = await _developerClient.PostAsJsonAsync(
            $"{ApiClient.V1}/repositories/{_developer.RepositoryId}/members",
            new { userId = _scrumMaster.UserId, defaultRole = "Developer", roleId });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "adding members needs ManageMembers, which the Developer role does not carry");
    }

    [Fact]
    public async Task ADeveloperCannotListUsers()
    {
        var response = await _developerClient.GetAsync($"{ApiClient.V1}/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "the caller is authenticated and known — they simply lack the privilege, " +
            "and a 401 here would send the client off to refresh a perfectly good token");
    }

    // ── 401 and 403 mean different things ───────────────────────────────────

    [Fact]
    public async Task MissingCredentialsGive401WhileMissingPrivilegeGives403()
    {
        // The two must never be conflated. 401 tells a client its credentials are absent or stale,
        // so the sensible reaction is to sign in again; 403 tells it the credentials are fine and
        // the answer is still no. Returning 401 for an authorisation failure sends the browser
        // into a pointless token refresh, and can mask a genuine permission problem as a session
        // problem.
        using var anonymous = ApiClient.Anonymous(_factory);

        var noToken = await anonymous.GetAsync($"{ApiClient.V1}/users");
        var noRight = await _developerClient.GetAsync($"{ApiClient.V1}/users");

        noToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "no credentials were presented");
        noRight.StatusCode.Should().Be(HttpStatusCode.Forbidden, "the credentials are valid, the privilege is not");
    }

    [Fact]
    public async Task ARepositoryTheCallerIsNotAMemberOfGives403NotA401()
    {
        // Membership failures run through the same path, so they are pinned here too.
        var response = await _developerClient.GetAsync(
            $"{ApiClient.V1}/repositories/{_scrumMaster.RepositoryId}/sprints");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Authenticated and allowed ───────────────────────────────────────────

    [Fact]
    public async Task ADeveloperCanReadTheirOwnRepository()
    {
        var response = await _developerClient.GetAsync(
            $"{ApiClient.V1}/repositories/{_developer.RepositoryId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "membership is enough to read the repository they belong to");
    }

    [Fact]
    public async Task ADeveloperCanReadTheirOwnWork()
    {
        var response = await _developerClient.GetAsync($"{ApiClient.V1}/my-work");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AScrumMasterCanCreateASprint()
    {
        using var client = await ApiClient.SignedInAsync(_factory, _scrumMaster);

        var response = await client.PostAsJsonAsync(
            $"{ApiClient.V1}/repositories/{_scrumMaster.RepositoryId}/sprints",
            new { name = "Authorised sprint", startDate = "2026-08-03", endDate = "2026-08-14" });

        response.StatusCode.Should().BeOneOf([HttpStatusCode.OK, HttpStatusCode.Created],
            "the Scrum Master role carries ManageSprint");
    }

    // ── The /internal routes: token instead of a JWT ────────────────────────

    [Fact]
    public async Task InternalRoutesRejectARequestWithNoToken()
    {
        using var anonymous = ApiClient.Anonymous(_factory);

        var response = await anonymous.GetAsync($"internal/v1/users/{_developer.UserId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "these routes are AllowAnonymous but guarded by a shared-token middleware");
    }

    [Fact]
    public async Task InternalRoutesRejectAWrongToken()
    {
        using var client = ApiClient.Anonymous(_factory);
        client.DefaultRequestHeaders.Add("X-Internal-Token", "wrong-token");

        var response = await client.GetAsync($"internal/v1/users/{_developer.UserId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InternalRoutesRejectAUserJwtInsteadOfTheToken()
    {
        // A normal signed-in user must not reach the internal surface just by being logged in.
        var response = await _developerClient.GetAsync($"internal/v1/users/{_developer.UserId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InternalRoutesAcceptTheConfiguredToken()
    {
        using var client = ApiClient.Anonymous(_factory);
        client.DefaultRequestHeaders.Add("X-Internal-Token", ApiFactory.InternalToken);

        var response = await client.GetAsync($"internal/v1/users/{_developer.UserId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
