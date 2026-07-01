using DASHBOARD.Application.SmartBoard.DTOs;
using MediatR;

namespace DASHBOARD.Application.SmartBoard.Queries.GetBoard;

/// <summary>Returns all columns of the smart board ordered by their display position, with card counts.</summary>
/// <param name="RepositoryId">The repository whose board to fetch.</param>
public sealed record GetBoardQuery(Guid RepositoryId) : IRequest<IReadOnlyList<SmartBoardColumnDto>>;
