using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace DASHBOARD.Domain.Enums;

/// <summary>Extension helpers for <see cref="MetadataKey"/>.</summary>
public static class MetadataKeyExtensions
{
    /// <summary>Returns the <see cref="DisplayAttribute.Name"/> of the enum value, falling back to <c>ToString()</c> when the attribute is absent.</summary>
    /// <param name="key">The metadata key.</param>
    /// <returns>The human-readable display name.</returns>
    public static string GetDisplayName(this MetadataKey key) =>
        typeof(MetadataKey)
            .GetField(key.ToString())
            ?.GetCustomAttribute<DisplayAttribute>()
            ?.Name
        ?? key.ToString();
}
