using System.Net.Http.Headers;
using System.Net.Http.Json;
using DASHBOARD.Application.Auth.Commands.Login;

namespace DASHBOARD.IntegrationTests.Infrastructure;

/// <summary>Creates HTTP clients for the in-memory API, signed in or anonymous.</summary>
/// <remarks>
/// Tokens are obtained by calling the real login endpoint rather than by minting a JWT locally.
/// That keeps the authentication path itself under test: if token issuance or the bearer
/// configuration breaks, every authorised test fails rather than silently passing on a hand-rolled
/// token the middleware would have rejected in production.
/// </remarks>
public static class ApiClient
{
    /// <summary>The API version segment every route carries.</summary>
    public const string V1 = "api/v1";

    /// <summary>Creates a client with no credentials.</summary>
    /// <param name="factory">The running API.</param>
    /// <returns>An anonymous <see cref="HttpClient"/>.</returns>
    public static HttpClient Anonymous(ApiFactory factory) => factory.CreateClient();

    /// <summary>Logs the seeded user in and returns a client carrying their bearer token.</summary>
    /// <param name="factory">The running API.</param>
    /// <param name="tenant">The tenant whose credentials to use.</param>
    /// <returns>An authenticated <see cref="HttpClient"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the login call does not succeed.</exception>
    public static async Task<HttpClient> SignedInAsync(ApiFactory factory, TenantSeed tenant)
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"{V1}/auth/login",
            new { orgAlias = tenant.OrgAlias, email = tenant.Email, password = tenant.Password });

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Seeded login failed with {(int)response.StatusCode}: {body}");
        }

        var session = await response.Content.ReadFromJsonAsync<LoginResult>()
            ?? throw new InvalidOperationException("Login returned no body.");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.AccessToken);

        return client;
    }
}
