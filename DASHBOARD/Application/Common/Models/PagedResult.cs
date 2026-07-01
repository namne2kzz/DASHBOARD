namespace DASHBOARD.Application.Common.Models;

/// <summary>Paged list result returned by list queries.</summary>
/// <typeparam name="T">The item type.</typeparam>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    /// <summary>Gets the total number of pages given the page size.</summary>
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>Gets whether a next page exists.</summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>Gets whether a previous page exists.</summary>
    public bool HasPreviousPage => Page > 1;
}
