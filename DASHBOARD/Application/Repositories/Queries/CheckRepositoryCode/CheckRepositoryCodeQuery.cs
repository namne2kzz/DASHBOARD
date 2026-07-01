using MediatR;

namespace DASHBOARD.Application.Repositories.Queries.CheckRepositoryCode;

/// <summary>Checks whether a repository code is available (not already in use).</summary>
/// <param name="Code">The candidate code to validate.</param>
public sealed record CheckRepositoryCodeQuery(string Code) : IRequest<bool>;
