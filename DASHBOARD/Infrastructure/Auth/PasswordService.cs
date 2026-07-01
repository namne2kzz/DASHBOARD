using System.Security.Cryptography;
using System.Text;
using DASHBOARD.Application.Common.Interfaces;

namespace DASHBOARD.Infrastructure.Auth;

/// <summary>
/// PBKDF2-SHA512 password hashing and verification.
/// Uses 310 000 iterations (NIST SP 800-63B recommendation for PBKDF2-SHA512),
/// a 32-byte (256-bit) cryptographically random salt per password,
/// and a 64-byte (512-bit) derived key stored as Base64.
/// </summary>
public sealed class PasswordService : IPasswordService
{
    private const int SaltSize       = 32;    // 256 bits
    private const int HashSize       = 64;    // 512 bits (full SHA-512 output)
    private const int Iterations     = 310_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;

    /// <summary>Hashes a plaintext password using PBKDF2-SHA512 with a freshly generated random salt.</summary>
    /// <param name="password">The plaintext password to hash — must not be empty.</param>
    /// <returns>Tuple of Base64-encoded <c>(Hash, Salt)</c>.</returns>
    public (string Hash, string Salt) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = DeriveHash(password, salt);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    /// <summary>Verifies a plaintext password against a stored PBKDF2-SHA512 hash using constant-time comparison.</summary>
    /// <param name="password">The plaintext password to check.</param>
    /// <param name="hash">The stored Base64-encoded PBKDF2 hash.</param>
    /// <param name="salt">The stored Base64-encoded salt used when hashing.</param>
    /// <returns><c>true</c> if the password matches; <c>false</c> otherwise.</returns>
    public bool VerifyPassword(string password, string hash, string salt)
    {
        var saltBytes     = Convert.FromBase64String(salt);
        var expectedHash  = Convert.FromBase64String(hash);
        var actualHash    = DeriveHash(password, saltBytes);

        // CryptographicOperations.FixedTimeEquals prevents timing side-channel attacks.
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static byte[] DeriveHash(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            Algorithm,
            HashSize);
}
