namespace DASHBOARD.Application.Orgs.DTOs;

/// <summary>Result returned to NKey after an organisation has been provisioned.</summary>
/// <param name="OrgId">Identifier of the created organisation, stored back on the licence key.</param>
/// <param name="OrgAlias">URL-safe tenant alias, echoed so the caller can build links.</param>
/// <param name="AdminUserId">Identifier of the first admin account created for this organisation.</param>
/// <param name="LoginUrl">Where the customer signs in once the password is set.</param>
/// <param name="SetPasswordUrl">
/// One-time link the customer must open to choose a password. Carries the raw token in the URL
/// fragment so it never reaches server, proxy or CDN logs.
/// </param>
/// <param name="SetPasswordExpiresAt">UTC instant after which <paramref name="SetPasswordUrl"/> stops working.</param>
public sealed record ProvisionOrganizationDto(
    Guid OrgId,
    string OrgAlias,
    Guid AdminUserId,
    string LoginUrl,
    string SetPasswordUrl,
    DateTime SetPasswordExpiresAt);
