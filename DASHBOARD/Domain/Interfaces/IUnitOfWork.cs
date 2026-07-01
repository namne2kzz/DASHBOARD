namespace DASHBOARD.Domain.Interfaces;

/// <summary>Unit of Work pattern — coordinates writing out changes across multiple repositories in a single transaction.</summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>Commits all tracked changes to the database atomically.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> CommitAsync(CancellationToken ct = default);
}
