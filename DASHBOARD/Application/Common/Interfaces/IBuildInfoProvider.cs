namespace DASHBOARD.Application.Common.Interfaces;

/// <summary>Build identity of the running API, read from its assembly metadata.</summary>
/// <remarks>
/// The values originate in DASHBOARD.csproj (VersionPrefix / BuildNumber / SourceRevisionId),
/// so a release is versioned by editing that file — nothing here needs changing.
/// </remarks>
public interface IBuildInfoProvider
{
    /// <summary>Gets the product version, e.g. "1.0.0". Never null; "unknown" when unreadable.</summary>
    string Version { get; }

    /// <summary>Gets the CI build number, or 0 for a local developer build.</summary>
    int BuildNumber { get; }

    /// <summary>Gets the full version including build number, e.g. "1.0.0+build.482".</summary>
    string FullVersion { get; }

    /// <summary>Gets the source commit the build came from, or null when CI did not supply one.</summary>
    string? CommitHash { get; }

    /// <summary>Gets the UTC time the assembly file was written, or null when it cannot be read.</summary>
    DateTime? BuildDate { get; }
}
