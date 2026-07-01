using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.SmartBoard.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.SmartBoard.Queries.GetBoard;

/// <summary>Handles <see cref="GetBoardQuery"/>: checks membership, loads columns ordered by position, and returns them.</summary>
public sealed class GetBoardQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<GetBoardQuery, IReadOnlyList<SmartBoardColumnDto>>
{
    /// <summary>Validates membership and returns board columns ordered by their display position.</summary>
    /// <param name="query">The board query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Board columns in display order.</returns>
    public async Task<IReadOnlyList<SmartBoardColumnDto>> Handle(GetBoardQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new UnauthorizedAccessException("You are not a member of this repository.");

        var columns = await db.Set<SmartBoardColumn>()
            .AsNoTracking()
            .Where(c => c.RepositoryId == query.RepositoryId)
            .OrderBy(c => c.Order)
            .ToListAsync(ct);

        return columns.Select(c => new SmartBoardColumnDto(
            c.Id, c.RepositoryId, c.Name, c.MappedState,
            c.WipLimit, c.WipMode, c.AgingLimitDays, c.Order)).ToList();
    }
}
