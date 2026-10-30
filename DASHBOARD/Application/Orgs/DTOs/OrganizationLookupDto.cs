namespace DASHBOARD.Application.Orgs.DTOs;

/// <summary>Confirms that a tenant alias exists, for the first step of sign-in.</summary>
/// <remarks>
/// Carries the alias and nothing else. The organisation's name, contact details and licence are
/// withheld on purpose: the caller is anonymous at this point, so the response may not describe a
/// tenant to someone who has not yet proved they belong to it.
/// </remarks>
/// <param name="Alias">The alias as stored, normalised to lower case.</param>
public sealed record OrganizationLookupDto(string Alias);
