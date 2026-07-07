using System.Text.RegularExpressions;

namespace DASHBOARD.Application.GitRepositories;

/// <summary>
/// Resolves a linked work item ID out of a commit message or PR title, kept out of
/// <c>OctokitGitHubIntegrationService</c> since it is domain-specific parsing, not a GitHub API concern
/// (mirrors the single-responsibility split already established by <see cref="GitRepoUrlParser"/>).
/// </summary>
internal static class GitWorkItemLinkResolver
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Finds the first work item reference in <paramref name="text"/>, preferring a <c>{code}-{number}</c>
    /// match (e.g. <c>DASH-12</c>) over a bare <c>#12</c> fallback.
    /// </summary>
    /// <param name="text">The commit message or PR title to scan.</param>
    /// <param name="projectCode">The project's <c>Repository.Code</c> (e.g. <c>DASH</c>).</param>
    /// <returns>The matched work item ID (e.g. <c>"DASH-12"</c> or <c>"12"</c>), or <c>null</c> if none is found.</returns>
    public static string? Resolve(string? text, string projectCode)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (!string.IsNullOrWhiteSpace(projectCode))
        {
            var codeMatch = Regex.Match(
                text, $@"\b{Regex.Escape(projectCode)}-(\d+)\b", RegexOptions.IgnoreCase, RegexTimeout);

            if (codeMatch.Success)
                return $"{projectCode.ToUpperInvariant()}-{codeMatch.Groups[1].Value}";
        }

        var bareMatch = Regex.Match(text, @"#(\d+)\b", RegexOptions.None, RegexTimeout);
        return bareMatch.Success ? bareMatch.Groups[1].Value : null;
    }
}
