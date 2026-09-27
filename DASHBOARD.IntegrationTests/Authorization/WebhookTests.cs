using System.Net;
using System.Text;
using DASHBOARD.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DASHBOARD.IntegrationTests.Authorization;

/// <summary>
/// Verifies that the GitHub webhook route refuses unverified deliveries.
/// </summary>
/// <remarks>
/// Like the internal API, this route is <c>[AllowAnonymous]</c> — GitHub cannot present a JWT — so
/// its only protection is an HMAC-SHA256 signature checked against the <c>WebhookSecret</c> of a
/// configured git connection. The danger is that an unverified POST reaches the handler and gets a
/// <c>GitSyncRequestedEvent</c> published on its behalf, which is a free lever for anyone who can
/// guess a repository id.
///
/// The test host configures no git connections, so every delivery here is rejected at the first
/// gate. That is a narrower claim than verifying the HMAC itself, but it is the claim that matters
/// for security: the route is closed by default and does not fall open when configuration is
/// absent. The signature comparison is unit-level logic and belongs in a unit test, not here.
/// </remarks>
[Collection(IntegrationTestCollection.Name)]
public sealed class WebhookTests(SqlServerFixture database) : IAsyncLifetime
{
    private readonly ApiFactory _factory = new(database);

    private TenantSeed _tenant = null!;

    /// <summary>Seeds one tenant so the route has a real repository id to be probed with.</summary>
    public async Task InitializeAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        _tenant = await TestData.SeedTenantAsync(_factory, $"hook{suffix}", "HK");
    }

    /// <summary>Disposes the API host.</summary>
    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private static StringContent Payload(string json) =>
        new(json, Encoding.UTF8, "application/json");

    [Fact]
    public async Task ADeliveryWithNoSignatureIsRejected()
    {
        using var caller = _factory.CreateClient();

        var response = await caller.PostAsync(
            $"{ApiClient.V1}/webhooks/github/{_tenant.RepositoryId}",
            Payload("""{"repository":{"full_name":"acme/project"}}"""));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ADeliveryWithAForgedSignatureIsRejected()
    {
        using var caller = _factory.CreateClient();
        caller.DefaultRequestHeaders.Add("X-Hub-Signature-256", "sha256=" + new string('a', 64));

        var response = await caller.PostAsync(
            $"{ApiClient.V1}/webhooks/github/{_tenant.RepositoryId}",
            Payload("""{"repository":{"full_name":"acme/project"}}"""));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ADeliveryForAnUnknownRepositoryIsRejected()
    {
        using var caller = _factory.CreateClient();

        var response = await caller.PostAsync(
            $"{ApiClient.V1}/webhooks/github/{Guid.NewGuid()}",
            Payload("""{"repository":{"full_name":"acme/project"}}"""));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "an unknown repository id must not be distinguishable from a signature failure");
    }

    [Fact]
    public async Task ARejectedDeliveryPublishesNothing()
    {
        // The whole point of the signature check is that an unverified caller cannot make the
        // system do work. Asserting on the status alone would miss a handler that rejects the
        // response but has already published.
        using var caller = _factory.CreateClient();

        await caller.PostAsync(
            $"{ApiClient.V1}/webhooks/github/{_tenant.RepositoryId}",
            Payload("""{"repository":{"full_name":"acme/project"}}"""));

        var published = _factory.Services.GetRequiredService<RecordingPublishEndpoint>();

        published.Messages.Should().BeEmpty("no sync may be triggered by an unverified delivery");
    }

    [Fact]
    public async Task TheWebhookRouteIgnoresABearerToken()
    {
        // Being signed in must neither help nor hinder: the route is judged on its signature only.
        using var signedIn = await ApiClient.SignedInAsync(_factory, _tenant);

        var response = await signedIn.PostAsync(
            $"{ApiClient.V1}/webhooks/github/{_tenant.RepositoryId}",
            Payload("""{"repository":{"full_name":"acme/project"}}"""));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
