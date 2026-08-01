namespace DASHBOARD.Application.Backlog.DTOs;

/// <summary>Summary of a bulk backlog operation, reporting how many items were affected and how many were skipped.</summary>
/// <param name="Affected">Number of items successfully updated or deleted.</param>
/// <param name="Skipped">Number of selected items that could not be processed (e.g. blocked by children).</param>
public sealed record BulkOperationResultDto(int Affected, int Skipped);
