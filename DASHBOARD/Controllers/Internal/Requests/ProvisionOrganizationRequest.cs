namespace DASHBOARD.Controllers.Internal.Requests;

/// <summary>Payload NKey sends to provision a new tenant organisation.</summary>
/// <param name="LicenceKey">Opaque licence key. Binds to at most one organisation.</param>
/// <param name="OrgName">Organisation display name.</param>
/// <param name="OrgAlias">URL-safe tenant prefix, unique system-wide.</param>
/// <param name="OrgAbout">Optional description; null or omitted is accepted.</param>
/// <param name="ContactEmail">Primary contact address for the organisation.</param>
/// <param name="AdminName">Full name of the first admin.</param>
/// <param name="AdminEmail">Email of the first admin; becomes their sign-in identity.</param>
/// <param name="LicenceDueDate">Payment due date copied from the licence.</param>
/// <param name="LicenceExpireDate">Licence expiry date copied from the licence.</param>
/// <param name="LicenceRepoCapacity">Maximum repositories allowed under this licence.</param>
public sealed record ProvisionOrganizationRequest(
    string LicenceKey,
    string OrgName,
    string OrgAlias,
    string? OrgAbout,
    string ContactEmail,
    string AdminName,
    string AdminEmail,
    DateTime LicenceDueDate,
    DateTime LicenceExpireDate,
    int LicenceRepoCapacity);
