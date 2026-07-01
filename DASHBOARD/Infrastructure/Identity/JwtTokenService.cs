using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Core.Constants;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DASHBOARD.Infrastructure.Identity;

/// <summary>Generates HMAC-SHA256 signed JWTs using settings from <see cref="JwtSettings"/>.</summary>
public sealed class JwtTokenService(IOptions<JwtSettings> options) : ITokenService
{
    private readonly JwtSettings _settings = options.Value;

    /// <summary>Creates and signs a JWT containing user identity claims.</summary>
    /// <param name="userId">The user's unique identifier, stored in the <c>sub</c> and <c>uid</c> claims.</param>
    /// <param name="email">The user's email address, stored in the <c>email</c> claim.</param>
    /// <param name="name">The user's display name, stored in the <c>name</c> claim.</param>
    /// <returns>A <see cref="TokenResult"/> containing the signed JWT, the <c>jti</c>, and the expiry time.</returns>
    public TokenResult GenerateToken(Guid userId, string email, string name)
    {
        var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jwtId       = Guid.NewGuid().ToString();
        var expiresAt   = DateTime.UtcNow.AddHours(_settings.ExpiresInHours);

        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, jwtId),
            new(AppConstants.UserIdClaim, userId.ToString()),
            new(AppConstants.UserNameClaim, name),
        ];

        var token = new JwtSecurityToken(
            issuer:            _settings.Issuer,
            audience:          _settings.Audience,
            claims:            claims,
            expires:           expiresAt,
            signingCredentials: credentials);

        return new TokenResult(
            Token:     new JwtSecurityTokenHandler().WriteToken(token),
            JwtId:     jwtId,
            ExpiresAt: expiresAt);
    }

    /// <summary>Generates a 64-byte (512-bit) cryptographically secure opaque refresh token, Base64-encoded.</summary>
    /// <returns>A Base64-encoded random token string. Hash with SHA-256 before storing in the database.</returns>
    public string GenerateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}
