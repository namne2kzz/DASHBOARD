using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Orgs.DTOs;
using MediatR;

namespace DASHBOARD.Application.Orgs.Commands.ProvisionOrganization;

/// <summary>
/// Creates a tenant organisation and its first admin account on behalf of NKey, then issues a
/// one-time link for that admin to choose a password.
/// </summary>
/// <remarks>
/// Called only from the internal API, authenticated by service token — never by an end user, since
/// at this point the organisation has no users who could sign in.
/// <para>Licence fields are a <b>snapshot</b> taken at activation: NKey remains the source of truth,
/// and editing a licence there does not retroactively change what was provisioned here.</para>
/// </remarks>
/// <param name="LicenceKey">Opaque licence key from NKey. Binds to at most one organisation.</param>
/// <param name="OrgName">Organisation display name.</param>
/// <param name="OrgAlias">URL-safe tenant prefix, unique system-wide.</param>
/// <param name="OrgAbout">Optional description; null or omitted is accepted.</param>
/// <param name="ContactEmail">Primary contact address for the organisation.</param>
/// <param name="AdminName">Full name of the first admin.</param>
/// <param name="AdminEmail">Email of the first admin; becomes their sign-in identity.</param>
/// <param name="LicenceDueDate">Payment due date copied from the licence.</param>
/// <param name="LicenceExpireDate">Licence expiry date copied from the licence.</param>
/// <param name="LicenceRepoCapacity">Maximum repositories allowed, enforced on repository creation.</param>
public sealed record ProvisionOrganizationCommand(
    string LicenceKey,
    string OrgName,
    string OrgAlias,
    string? OrgAbout,
    string ContactEmail,
    string AdminName,
    string AdminEmail,
    DateTime LicenceDueDate,
    DateTime LicenceExpireDate,
    int LicenceRepoCapacity) : IRequest<Result<ProvisionOrganizationDto>>;
