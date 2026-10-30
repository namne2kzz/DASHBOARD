namespace DASHBOARD.Application.Orgs.DTOs;

/// <summary>How much of an organisation's licence allowance is currently in use.</summary>
/// <remarks>
/// Read by NKey to show capacity on the licence record and to warn before a customer runs out.
/// The capacity and expiry here are the snapshot taken at provisioning — NKey remains the source of
/// truth for what was sold.
/// </remarks>
/// <param name="OrgId">Organisation identifier.</param>
/// <param name="Alias">URL-safe tenant alias.</param>
/// <param name="Name">Organisation display name.</param>
/// <param name="RepoCount">
/// Repositories counted against the licence. Includes archived ones, because
/// <c>CreateRepositoryCommandHandler</c> counts them too — the two numbers must agree or NKey would
/// show capacity left while creation is already blocked.
/// </param>
/// <param name="RepoCapacity">Maximum repositories the licence allows.</param>
/// <param name="UserCount">Active (non-deleted) user accounts in the organisation.</param>
/// <param name="LicenceDueDate">Payment due date from the licence snapshot.</param>
/// <param name="LicenceExpireDate">Expiry date from the licence snapshot.</param>
public sealed record OrganizationUsageDto(
    Guid OrgId,
    string Alias,
    string Name,
    int RepoCount,
    int RepoCapacity,
    int UserCount,
    DateTime LicenceDueDate,
    DateTime LicenceExpireDate);
