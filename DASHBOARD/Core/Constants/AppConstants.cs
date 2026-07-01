namespace DASHBOARD.Core.Constants;

/// <summary>Application-wide string constants shared across all layers.</summary>
public static class AppConstants
{
    /// <summary>The default SQL Server schema used for all tables.</summary>
    public const string DefaultSchema = "dbo";

    /// <summary>JWT claim type carrying the user's <see cref="Guid"/> identifier.</summary>
    public const string UserIdClaim = "uid";

    /// <summary>JWT claim type carrying the user's display name.</summary>
    public const string UserNameClaim = "name";

    /// <summary>Named CORS policy allowing requests from the Angular dev server.</summary>
    public const string AngularCorsPolicy = "AllowAngular";
}
