using System.Reflection;
using DASHBOARD.Application.Common.Interfaces;

namespace DASHBOARD.Infrastructure.Services;

/// <summary>
/// Reads build identity off the entry assembly's metadata, which the SDK stamps from the
/// properties in DASHBOARD.csproj at compile time.
/// </summary>
/// <remarks>
/// Everything is resolved once in the constructor: the values cannot change while the process
/// runs, so this is registered as a singleton and the reflection cost is paid at startup.
/// </remarks>
public sealed class AssemblyBuildInfoProvider : IBuildInfoProvider
{
    /// <summary>Resolves the version, build number, commit and build date from assembly metadata.</summary>
    public AssemblyBuildInfoProvider()
    {
        var assembly = Assembly.GetEntryAssembly();

        // InformationalVersion carries the full "1.0.0+commit" string the SDK composes;
        // AssemblyVersion is the four-part numeric fallback whose last part is the build number.
        var informational = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var assemblyVersion = assembly?.GetName().Version;

        var plusIndex = informational?.IndexOf('+') ?? -1;

        Version = plusIndex > 0
            ? informational![..plusIndex]
            : informational ?? assemblyVersion?.ToString(3) ?? "unknown";

        CommitHash = plusIndex > 0 && plusIndex + 1 < informational!.Length
            ? informational[(plusIndex + 1)..]
            : null;

        // Fourth part of AssemblyVersion — set from the BuildNumber property in the csproj.
        BuildNumber = assemblyVersion?.Revision > 0 ? assemblyVersion.Revision : 0;

        FullVersion = BuildNumber > 0 ? $"{Version}+build.{BuildNumber}" : Version;

        BuildDate = ReadBuildDate(assembly);
    }

    /// <inheritdoc />
    public string Version { get; }

    /// <inheritdoc />
    public int BuildNumber { get; }

    /// <inheritdoc />
    public string FullVersion { get; }

    /// <inheritdoc />
    public string? CommitHash { get; }

    /// <inheritdoc />
    public DateTime? BuildDate { get; }

    /// <summary>Last write time of the assembly file, used as the build timestamp.</summary>
    /// <param name="assembly">The entry assembly, or null when it cannot be resolved.</param>
    /// <returns>UTC build time, or null when the file cannot be read (e.g. a single-file publish).</returns>
    private static DateTime? ReadBuildDate(Assembly? assembly)
    {
        try
        {
            var path = assembly?.Location;
            return string.IsNullOrEmpty(path) ? null : File.GetLastWriteTimeUtc(path);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
