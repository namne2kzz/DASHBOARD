namespace DASHBOARD.Controllers.Repositories.Requests;

/// <summary>HTTP request body for creating a metadata catalog entry.</summary>
/// <param name="Key">The metadata key name (e.g. "RepoRole").</param>
/// <param name="Value">The value to add (e.g. "1.4.1").</param>
/// <param name="IsGlobal">When true, the entry is shared across all repositories (GlobalAdmin only).</param>
public sealed record AddMetadataRequest(string Key, string Value, bool IsGlobal = false);

/// <summary>HTTP request body for updating a metadata catalog entry's value.</summary>
/// <param name="Value">The new value.</param>
public sealed record UpdateMetadataRequest(string Value);
