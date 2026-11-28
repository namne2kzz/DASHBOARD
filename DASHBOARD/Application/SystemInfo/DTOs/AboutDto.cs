namespace DASHBOARD.Application.SystemInfo.DTOs;

/// <summary>Health of one external dependency the API talks to.</summary>
/// <param name="Name">Display name, e.g. "SQL Server".</param>
/// <param name="Kind">Stable machine key, e.g. "database" — the UI picks its icon from this.</param>
/// <param name="Status">
/// "healthy" (probed, responded), "unreachable" (probed, failed or timed out), or
/// "configured" (wired up but deliberately not probed — see the handler for why).
/// </param>
/// <param name="Detail">Short human-readable note: the target host, or why the check failed.</param>
/// <param name="LatencyMs">Round-trip time of the probe, or null when no probe ran.</param>
public sealed record ServiceStatusDto(
    string Name,
    string Kind,
    string Status,
    string? Detail,
    long?  LatencyMs);

/// <summary>Everything the About dialog shows: what this build is, and what it is connected to.</summary>
/// <param name="Application">Product name.</param>
/// <param name="Version">Product version from the API assembly, e.g. "1.0.0".</param>
/// <param name="BuildNumber">CI build number, or 0 for a local developer build.</param>
/// <param name="FullVersion">Version and build number together, e.g. "1.0.0+build.482".</param>
/// <param name="CommitHash">Source commit of the build, or null when CI did not supply one.</param>
/// <param name="BuildDate">UTC timestamp the API assembly was written.</param>
/// <param name="Environment">ASPNETCORE_ENVIRONMENT, e.g. "Development".</param>
/// <param name="ServerTimeUtc">Server clock at the moment of the request.</param>
/// <param name="Runtime">.NET runtime version serving the request.</param>
/// <param name="Services">One entry per external dependency.</param>
public sealed record AboutDto(
    string Application,
    string Version,
    int BuildNumber,
    string FullVersion,
    string? CommitHash,
    DateTime? BuildDate,
    string Environment,
    DateTime ServerTimeUtc,
    string Runtime,
    IReadOnlyList<ServiceStatusDto> Services);
