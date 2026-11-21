using System.Text.RegularExpressions;

namespace DASHBOARD.Application.Common.Helpers;

/// <summary>Finds the stored images a Markdown comment body points at.</summary>
/// <remarks>
/// Pasted and dropped images live in the attachment bucket but, unlike real attachments, have no row
/// of their own — the body is the only record that they are in use. Editing or deleting a comment is
/// therefore the only moment their objects can be reclaimed.
/// </remarks>
public static partial class CommentImageHelper
{
    /// <summary>Matches the URL inside a Markdown image, <c>![alt](url)</c>.</summary>
    [GeneratedRegex(@"!\[[^\]]*\]\((?<url>[^)\s]+)", RegexOptions.Compiled)]
    private static partial Regex ImageRegex();

    /// <summary>
    /// Returns the object keys of every image in <paramref name="body"/> that lives in this
    /// deployment's attachment bucket.
    /// </summary>
    /// <param name="body">The Markdown comment body.</param>
    /// <param name="publicBaseUrl">The MinIO public base URL.</param>
    /// <param name="bucket">The attachment bucket name.</param>
    /// <returns>Distinct object keys; empty when the body has no images of ours.</returns>
    /// <remarks>
    /// Images hosted elsewhere are ignored: a reader may paste any URL, and deleting something we do
    /// not own is both wrong and impossible.
    /// </remarks>
    public static IReadOnlyCollection<string> ExtractObjectKeys(string? body, string publicBaseUrl, string bucket)
    {
        if (string.IsNullOrWhiteSpace(body))
            return [];

        var prefix = $"{publicBaseUrl.TrimEnd('/')}/{bucket}/";

        return ImageRegex().Matches(body)
            .Select(m => m.Groups["url"].Value)
            .Where(url => url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            // Strip any query string: a presigned GET carries its signature after the key.
            .Select(url => url[prefix.Length..].Split('?')[0])
            .Where(key => key.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Returns the object keys present in <paramref name="oldBody"/> but gone from
    /// <paramref name="newBody"/> — the images an edit removed.
    /// </summary>
    /// <param name="oldBody">The body before the edit.</param>
    /// <param name="newBody">The body after the edit.</param>
    /// <param name="publicBaseUrl">The MinIO public base URL.</param>
    /// <param name="bucket">The attachment bucket name.</param>
    /// <returns>Object keys that are now unreferenced by this comment.</returns>
    public static IReadOnlyCollection<string> FindRemoved(string? oldBody, string? newBody, string publicBaseUrl, string bucket)
    {
        var before = ExtractObjectKeys(oldBody, publicBaseUrl, bucket);
        if (before.Count == 0)
            return [];

        var after = ExtractObjectKeys(newBody, publicBaseUrl, bucket).ToHashSet(StringComparer.Ordinal);
        return before.Where(key => !after.Contains(key)).ToList();
    }
}
