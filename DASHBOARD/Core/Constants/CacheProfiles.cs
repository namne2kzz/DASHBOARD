namespace DASHBOARD.Core.Constants;

/// <summary>
/// The <c>Cache-Control</c> lifetimes an API read may opt into, grouped by how fast the data changes.
/// </summary>
/// <remarks>
/// Every profile is <c>private</c>: responses are scoped to the calling user, so a shared proxy must
/// never store them. Pick the profile from how stale the UI can tolerate the data being, not from how
/// expensive the query is — an expensive query that must stay fresh belongs in a server-side cache
/// (Redis), not in a longer <c>max-age</c>.
/// </remarks>
public static class CacheProfiles
{
    /// <summary>
    /// Reference data that changes on a human timescale (enum catalogs, role definitions): served
    /// straight from the browser cache for an hour with no request at all.
    /// </summary>
    public const int ReferenceSeconds = 3600;

    /// <summary>
    /// Semi-static, per-repository data (member lists, metadata catalogs): short enough that a
    /// membership change surfaces within minutes.
    /// </summary>
    public const int SemiStaticSeconds = 300;

    /// <summary>
    /// Hot reads that must look live (boards, backlog, work items): the browser may store the body
    /// but must revalidate every time, so the ETag decides whether bytes travel.
    /// </summary>
    public const int RevalidateSeconds = 0;
}
