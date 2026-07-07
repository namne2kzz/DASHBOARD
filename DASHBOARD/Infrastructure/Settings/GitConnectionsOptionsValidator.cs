using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace DASHBOARD.Infrastructure.Settings;

/// <summary>
/// Validates <see cref="GitConnectionsOptions"/> at startup so a misconfigured
/// <c>GitConnections</c> section fails fast with a clear error instead of surfacing
/// a confusing failure later when a project's Repos tab is opened.
/// </summary>
public sealed partial class GitConnectionsOptionsValidator : IValidateOptions<GitConnectionsOptions>
{
    [GeneratedRegex(@"^https://github\.com/[\w.-]+/[\w.-]+(\.git)?/?$")]
    private static partial Regex RepoUrlPattern();

    /// <summary>
    /// Validates every configured connection entry: <c>RepoUrl</c> must be a well-formed GitHub HTTPS URL,
    /// <c>Token</c> must be non-empty, and <c>RepoUrl</c> must not be duplicated within the same project code's list.
    /// Note: a code with no entry marked <c>IsPrimary</c> is a documented fallback (the first entry is used),
    /// not a validation failure.
    /// </summary>
    /// <param name="name">The named options instance being validated (unused — this options type is not named).</param>
    /// <param name="options">The bound <see cref="GitConnectionsOptions"/> to validate.</param>
    /// <returns>A successful result, or a failure listing every violation found across all project codes.</returns>
    public ValidateOptionsResult Validate(string? name, GitConnectionsOptions options)
    {
        var errors = new List<string>();

        foreach (var (code, entries) in options)
        {
            if (entries is null) continue;

            var seenRepoUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.RepoUrl) || !RepoUrlPattern().IsMatch(entry.RepoUrl))
                {
                    errors.Add($"GitConnections:{code}: RepoUrl '{entry.RepoUrl}' is not a valid GitHub HTTPS URL.");
                }
                else if (!seenRepoUrls.Add(entry.RepoUrl))
                {
                    errors.Add($"GitConnections:{code}: duplicate RepoUrl '{entry.RepoUrl}' in the same project's connection list.");
                }

                if (string.IsNullOrWhiteSpace(entry.Token))
                {
                    errors.Add($"GitConnections:{code}: Token must not be empty (RepoUrl '{entry.RepoUrl}').");
                }
            }
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
