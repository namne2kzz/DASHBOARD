using System.IdentityModel.Tokens.Jwt;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Core.Constants;
using Microsoft.AspNetCore.Http;

namespace DASHBOARD.Infrastructure.Auth;

/// <summary>Reads the authenticated user's identity from the current HTTP request's JWT claims.</summary>
public sealed class CurrentUserService : ICurrentUserService
{
    /// <summary>Initializes a new <see cref="CurrentUserService"/> by extracting claims from the HTTP context.</summary>
    /// <param name="accessor">Provides access to the current <see cref="HttpContext"/>.</param>
    public CurrentUserService(IHttpContextAccessor accessor)
    {
        var claims = accessor.HttpContext?.User;
        IsAuthenticated = claims?.Identity?.IsAuthenticated ?? false;

        if (!IsAuthenticated) return;

        var rawId = claims!.FindFirst(AppConstants.UserIdClaim)?.Value;
        UserId = Guid.TryParse(rawId, out var id) ? id : null;
        var rawOrgId = claims.FindFirst(AppConstants.OrgIdClaim)?.Value;
        OrgId  = Guid.TryParse(rawOrgId, out var orgId) ? orgId : null;
        Email  = claims.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
        Name   = claims.FindFirst(AppConstants.UserNameClaim)?.Value;
        JwtId  = claims.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
    }

    /// <inheritdoc/>
    public Guid? UserId { get; }

    /// <inheritdoc/>
    public Guid? OrgId { get; }

    /// <inheritdoc/>
    public string? Email { get; }

    /// <inheritdoc/>
    public string? Name { get; }

    /// <inheritdoc/>
    public string? JwtId { get; }

    /// <inheritdoc/>
    public bool IsAuthenticated { get; }
}
