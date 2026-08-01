namespace DASHBOARD.Application.Search.DTOs;

/// <summary>A single global-search hit, normalised across entity types for a unified results list.</summary>
/// <param name="Kind">Entity kind — "task", "backlog", or "wiki" — used by the UI to group and route.</param>
/// <param name="Id">The entity identifier.</param>
/// <param name="Title">The primary display text (item/page title).</param>
/// <param name="Subtitle">Secondary context line (e.g. "DASH-12 · Bug", "User Story", "Wiki").</param>
public sealed record SearchResultItemDto(
    string  Kind,
    Guid    Id,
    string  Title,
    string? Subtitle);
