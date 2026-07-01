using System.ComponentModel.DataAnnotations;

namespace DASHBOARD.Domain.Enums;

/// <summary>
/// Well-known metadata keys for the repository catalog.
/// Each value carries a <see cref="DisplayAttribute"/> as the human-readable label —
/// use <c>MetadataKeyExtensions.GetDisplayName()</c> instead of switch statements.
/// Stored as a string in the database. Add new values here without a schema migration.
/// </summary>
public enum MetadataKey
{
    [Display(Name = "Fixed In Version")]
    FixedInVersion,

    [Display(Name = "Implemented In Build")]
    ImplementedInBuild,

    [Display(Name = "Release Notes")]
    ReleaseNotes,

    [Display(Name = "QA Notes")]
    QaNotes,

    [Display(Name = "Design Doc URL")]
    DesignDocUrl,

    [Display(Name = "External Reference")]
    ExternalReference,

    [Display(Name = "Components")]
    Components,

    [Display(Name = "Labels")]
    Labels,

    [Display(Name = "Team Role")]
    RepoRole,
}
