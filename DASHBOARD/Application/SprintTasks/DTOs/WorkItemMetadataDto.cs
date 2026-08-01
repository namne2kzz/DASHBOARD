namespace DASHBOARD.Application.SprintTasks.DTOs;

/// <summary>A metadata catalog value assigned to a work item.</summary>
/// <param name="MetadataId">The repository metadata value identifier.</param>
/// <param name="Key">The metadata key enum value (int).</param>
/// <param name="KeyName">The human-readable key display name (e.g. "Labels").</param>
/// <param name="Value">The assigned value (e.g. "Defer").</param>
public sealed record WorkItemMetadataDto(
    Guid   MetadataId,
    int    Key,
    string KeyName,
    string Value);
