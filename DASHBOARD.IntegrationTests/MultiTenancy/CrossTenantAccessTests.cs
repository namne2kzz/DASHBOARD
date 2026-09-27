using System.Net;
using System.Net.Http.Json;
using DASHBOARD.Domain.Enums;
using DASHBOARD.IntegrationTests.Infrastructure;

namespace DASHBOARD.IntegrationTests.MultiTenancy;

/// <summary>
/// Verifies that a signed-in user of one organization cannot reach another organization's data.
/// </summary>
/// <remarks>
/// This is the most serious failure this system could have, and unit tests cannot prove it: they
/// exercise a handler with a hand-built permission context, whereas a tenant leak would come from
/// the layers around it — a route parameter trusted without a membership check, a filter missing on
/// a controller, a query scoped by id but not by organization.
///
/// So every test here goes through the genuine pipeline: a real token for tenant A, a real request
/// naming a resource of tenant B. Anything other than a refusal is a leak.
/// </remarks>
[Collection(IntegrationTestCollection.Name)]
public sealed class CrossTenantAccessTests(SqlServerFixture database) : IAsyncLifetime
{
    private readonly ApiFactory _factory = new(database);

    private TenantSeed _acme  = null!;
    private TenantSeed _globex = null!;
    private HttpClient _acmeClient = null!;

    /// <summary>Seeds two unrelated tenants and signs in as the first one.</summary>
    public async Task InitializeAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        _acme   = await TestData.SeedTenantAsync(_factory, $"acme{suffix}",   "ACM");
        _globex = await TestData.SeedTenantAsync(_factory, $"globex{suffix}", "GLX");

        _acmeClient = await ApiClient.SignedInAsync(_factory, _acme);
    }

    /// <summary>Disposes the client and the API host.</summary>
    public Task DisposeAsync()
    {
        _acmeClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Statuses that count as a refusal. A permission or membership failure is a 403; a resource
    /// the caller may not even know exists can legitimately come back as 404. 400 is deliberately
    /// absent — a cross-tenant request is not a malformed one, and answering it with 400 would tell
    /// a client to fix its payload when the real answer is "not yours".
    /// </summary>
    private static readonly HttpStatusCode[] Refused =
    [
        HttpStatusCode.Forbidden,
        HttpStatusCode.NotFound,
    ];

    // ── Reading another tenant's repository ─────────────────────────────────

    [Fact]
    public async Task GetRepository_OfAnotherTenant_IsRefused()
    {
        var response = await _acmeClient.GetAsync($"{ApiClient.V1}/repositories/{_globex.RepositoryId}");

        response.StatusCode.Should().BeOneOf(Refused);
    }

    [Fact]
    public async Task ListRepositories_ShowsOnlyTheCallersOwnOrganization()
    {
        var response = await _acmeClient.GetAsync($"{ApiClient.V1}/repositories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(_globex.RepositoryCode,
            "the other tenant's repository must not appear in the list at all");
        body.Should().Contain(_acme.RepositoryCode);
    }

    // ── Reading another tenant's child collections ──────────────────────────

    [Theory]
    [InlineData("members")]
    [InlineData("sprints")]
    [InlineData("backlog")]
    [InlineData("roles")]
    public async Task ListingAChildCollectionOfAnotherTenant_IsRefused(string segment)
    {
        var response = await _acmeClient.GetAsync(
            $"{ApiClient.V1}/repositories/{_globex.RepositoryId}/{segment}");

        response.StatusCode.Should().BeOneOf(Refused,
            $"'{segment}' of another tenant's repository is not the caller's data");
    }

    // ── Writing into another tenant ─────────────────────────────────────────

    [Fact]
    public async Task CreatingASprintInAnotherTenantsRepository_IsRefused()
    {
        var response = await _acmeClient.PostAsJsonAsync(
            $"{ApiClient.V1}/repositories/{_globex.RepositoryId}/sprints",
            new
            {
                name      = "Injected sprint",
                startDate = "2026-06-01",
                endDate   = "2026-06-12",
            });

        response.StatusCode.Should().BeOneOf(Refused);

        await using var db = database.CreateContext();
        var leaked = db.Sprints.Any(s => s.RepositoryId == _globex.RepositoryId);
        leaked.Should().BeFalse("no row may be written into the other tenant");
    }

    [Fact]
    public async Task CreatingABacklogItemInAnotherTenantsRepository_IsRefused()
    {
        var response = await _acmeClient.PostAsJsonAsync(
            $"{ApiClient.V1}/repositories/{_globex.RepositoryId}/backlog",
            new
            {
                // The API has no string-enum converter, so an enum must go over the wire as its numeric
                // value — a name here fails model binding with 400 and never reaches the handler, which
                // would leave the tenant check unproven.
                type               = (int)BacklogItemType.UserStory,
                title              = "Injected item",
                acceptanceCriteria = string.Empty,
            });

        response.StatusCode.Should().BeOneOf(Refused);

        await using var db = database.CreateContext();
        db.BacklogItems.Any(b => b.RepositoryId == _globex.RepositoryId)
          .Should().BeFalse();
    }

    [Fact]
    public async Task CreatingARoleInAnotherTenantsRepository_IsRefused()
    {
        var response = await _acmeClient.PostAsJsonAsync(
            $"{ApiClient.V1}/repositories/{_globex.RepositoryId}/roles",
            new
            {
                name             = "Injected role",
                description      = "Should never exist",
                permissions      = new[] { (int)SystemFunction.ViewRepository },
            });

        response.StatusCode.Should().BeOneOf(Refused);

        await using var db = database.CreateContext();
        db.Roles.Any(r => r.RepositoryId == _globex.RepositoryId && r.Name == "Injected role")
          .Should().BeFalse();
    }

    [Fact]
    public async Task AddingAMemberToAnotherTenantsRepository_IsRefused()
    {
        await using var db = database.CreateContext();
        var globexRoleId = db.Roles.First(r => r.RepositoryId == _globex.RepositoryId).Id;

        var response = await _acmeClient.PostAsJsonAsync(
            $"{ApiClient.V1}/repositories/{_globex.RepositoryId}/members",
            new
            {
                userId      = _acme.UserId,
                defaultRole = "Developer",
                roleId      = globexRoleId,
            });

        response.StatusCode.Should().BeOneOf(Refused);

        db.RepositoryMembers.Any(m => m.RepositoryId == _globex.RepositoryId && m.UserId == _acme.UserId)
          .Should().BeFalse("a user must not be able to insert themselves into another tenant");
    }

    // ── Editing another tenant ──────────────────────────────────────────────

    [Fact]
    public async Task UpdatingAnotherTenantsRepository_IsRefused()
    {
        var response = await _acmeClient.PutAsJsonAsync(
            $"{ApiClient.V1}/repositories/{_globex.RepositoryId}",
            new { name = "Renamed by outsider", description = "Should not apply" });

        response.StatusCode.Should().BeOneOf(Refused);

        await using var db = database.CreateContext();
        var repository = db.Repositories.Single(r => r.Id == _globex.RepositoryId);
        repository.Name.Should().NotBe("Renamed by outsider");
    }

    // ── Anonymous access ────────────────────────────────────────────────────

    [Theory]
    [InlineData("members")]
    [InlineData("sprints")]
    [InlineData("backlog")]
    [InlineData("roles")]
    public async Task AnonymousRequestsAreRejected(string segment)
    {
        using var anonymous = ApiClient.Anonymous(_factory);

        var response = await anonymous.GetAsync(
            $"{ApiClient.V1}/repositories/{_acme.RepositoryId}/{segment}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "no endpoint under a repository is public");
    }
}
