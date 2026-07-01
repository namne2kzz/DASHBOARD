using System.Security.Cryptography;
using System.Text;
using DASHBOARD.Application.Common.Interfaces;

namespace DASHBOARD.Infrastructure.Identity;

/// <summary>Generates and hashes cryptographically secure one-time invite tokens using SHA-256.</summary>
internal sealed class InvitationTokenService : IInvitationTokenService
{
    private const int TokenByteLength = 32;

    /// <summary>Generates a new URL-safe Base64 raw token and its SHA-256 hex hash.</summary>
    /// <returns>A tuple of the raw token to embed in the invite link and the hash to store in the DB.</returns>
    public (string RawToken, string TokenHash) Generate()
    {
        var bytes    = RandomNumberGenerator.GetBytes(TokenByteLength);
        var rawToken = Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('='); // URL-safe Base64

        return (rawToken, Hash(rawToken));
    }

    /// <summary>Computes the SHA-256 hex hash of a raw token for database lookup.</summary>
    /// <param name="rawToken">The raw token from the invite link query string.</param>
    /// <returns>64-character lowercase hex SHA-256 hash.</returns>
    public string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
