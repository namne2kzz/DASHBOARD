namespace DASHBOARD.Application.Common.Interfaces;

/// <summary>Exposes the identity of the currently authenticated user extracted from the JWT claims. Implementation lives in Infrastructure.</summary>
public interface ICurrentUserService
{
    /// <summary>Gets the authenticated user's ID from the <c>uid</c> JWT claim. Null when not authenticated.</summary>
    Guid? UserId { get; }

    /// <summary>Gets the authenticated user's email from the <c>email</c> JWT claim. Null when not authenticated.</summary>
    string? Email { get; }

    /// <summary>Gets the authenticated user's display name from the <c>name</c> JWT claim. Null when not authenticated.</summary>
    string? Name { get; }

    /// <summary>Gets the <c>jti</c> JWT ID of the current token. Used for server-side revocation. Null when not authenticated.</summary>
    string? JwtId { get; }

    /// <summary>Gets whether the current request is authenticated.</summary>
    bool IsAuthenticated { get; }
}
