namespace DASHBOARD.Application.Common.Interfaces;

/// <summary>Contract for PBKDF2-SHA512 password hashing and verification. Implementation lives in Infrastructure.</summary>
public interface IPasswordService
{
    /// <summary>Derives a PBKDF2-SHA512 hash for the given plaintext password using a newly generated random salt.</summary>
    /// <param name="password">The plaintext password to hash.</param>
    /// <returns>A tuple of <c>(hash, salt)</c> both encoded as Base64 strings.</returns>
    (string Hash, string Salt) HashPassword(string password);

    /// <summary>Verifies a plaintext password against a stored PBKDF2-SHA512 hash and salt.</summary>
    /// <param name="password">The plaintext password to verify.</param>
    /// <param name="hash">The stored Base64-encoded hash.</param>
    /// <param name="salt">The stored Base64-encoded salt used during hashing.</param>
    /// <returns><c>true</c> if the password matches; <c>false</c> otherwise. Uses constant-time comparison to prevent timing attacks.</returns>
    bool VerifyPassword(string password, string hash, string salt);
}
