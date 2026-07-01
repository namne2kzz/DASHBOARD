using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Repositories.Queries.CheckRepositoryCode;

/// <summary>Handles <see cref="CheckRepositoryCodeQuery"/>: returns true when the code is not in use by any existing repository.</summary>
public sealed class CheckRepositoryCodeQueryHandler(
    IApplicationDbContext db) : IRequestHandler<CheckRepositoryCodeQuery, bool>
{
    /// <summary>Checks whether the code is already taken across all non-deleted repositories.</summary>
    /// <param name="query">The check query containing the candidate code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if the code is available; <c>false</c> if already in use.</returns>
    public async Task<bool> Handle(CheckRepositoryCodeQuery query, CancellationToken ct)
    {
        var codeUpper = query.Code.ToUpperInvariant();
        var taken = await db.Set<Repository>()
            .AsNoTracking()
            .AnyAsync(r => r.Code == codeUpper, ct);
        return !taken;
    }
}
