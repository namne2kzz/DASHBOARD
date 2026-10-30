using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Orgs.DTOs;
using MediatR;

namespace DASHBOARD.Application.Orgs.Queries.GetOrganizationByAlias;

/// <summary>
/// Resolves a tenant alias typed on the first step of sign-in, before any credentials are given.
/// </summary>
/// <remarks>
/// Anonymous by necessity: the user cannot authenticate until they have told us which organisation
/// they belong to. Confirming that an alias exists is a deliberate trade — it lets somebody probe
/// for valid aliases, which is why the endpoint is rate limited and the response deliberately
/// carries no organisation name, contact details or any other tenant data.
/// </remarks>
/// <param name="Alias">The URL-safe tenant alias the user typed.</param>
public sealed record GetOrganizationByAliasQuery(string Alias) : IRequest<Result<OrganizationLookupDto>>;
