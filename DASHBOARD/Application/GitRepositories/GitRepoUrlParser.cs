namespace DASHBOARD.Application.GitRepositories;

/// <summary>Parses the owner login and repository name out of a configured GitHub HTTPS clone URL. Shared by both GitRepositories query handlers so the parsing rule lives in exactly one place.</summary>
internal static class GitRepoUrlParser
{
    /// <summary>
    /// Extracts <c>(OwnerLogin, RepoName)</c> from a URL such as <c>https://github.com/owner/repo.git</c>,
    /// stripping the trailing <c>.git</c> suffix and/or slash.
    /// </summary>
    /// <param name="repoUrl">The full HTTPS clone URL, validated at startup by <c>GitConnectionsOptionsValidator</c>.</param>
    /// <returns>The owner login and repository name segments.</returns>
    public static (string OwnerLogin, string RepoName) Parse(string repoUrl)
    {
        var trimmed = repoUrl.Trim().TrimEnd('/');

        if (trimmed.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^4];

        var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments.Length >= 2
            ? (segments[^2], segments[^1])
            : (string.Empty, string.Empty);
    }
}
