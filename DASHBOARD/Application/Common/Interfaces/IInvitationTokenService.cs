namespace DASHBOARD.Application.Common.Interfaces;

/// <summary>Generates and hashes cryptographically secure one-time invite tokens.</summary>
public interface IInvitationTokenService
{
    /// <summary>Generates a new URL-safe raw token and its SHA-256 hash.</summary>
    /// <returns>
    /// <c>RawToken</c> — embed in the invite link query string (never store this).<br/>
    /// <c>TokenHash</c> — store in the database for later lookup.
    /// </returns>
    (string RawToken, string TokenHash) Generate();

    /// <summary>Hashes a raw token for database lookup without exposing the plaintext.</summary>
    /// <param name="rawToken">The raw token extracted from the invite link.</param>
    /// <returns>The SHA-256 hex hash of the token.</returns>
    string Hash(string rawToken);
}
