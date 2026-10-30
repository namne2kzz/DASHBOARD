using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Orgs.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Orgs.Queries.GetOrganizationByAlias;

// <summary>Handles <see cref="GetOrganizationByAliasQuery"/>.</summary>
internal sealed class GetOrganizationByAliasQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetOrganizationByAliasQuery, Result<OrganizationLookupDto>>
{
    // <summary>Looks the alias up and reports only whether it resolves to an organisation.</summary>
    // <param name="request">The query carrying the alias to resolve.</param>
    // <param name="ct">Cancellation token.</param>
    // <returns>The alias on success; a failure with a generic message when it does not exist.</returns>
    public async Task<Result<OrganizationLookupDto>> Handle(
        GetOrganizationByAliasQuery request,
        CancellationToken ct)
    {
        var alias = request.Alias.Trim().ToLowerInvariant();

        var exists = await db.Set<Organization>()
            .AsNoTracking()
            .AnyAsync(o => o.Alias == alias, ct);

        // Projecting nothing but the alias keeps the organisation's own data out of an anonymous
        // response, even as columns are added to the entity later.
        return exists
            ? Result<OrganizationLookupDto>.Success(new OrganizationLookupDto(alias))
            : Result<OrganizationLookupDto>.Failure("No organization found with that name.");
    }
}
