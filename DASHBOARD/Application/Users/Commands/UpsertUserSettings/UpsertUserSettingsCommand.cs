using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Users.Commands.UpsertUserSettings;

/// <summary>
/// Batch-upserts one or more preference settings for the currently authenticated user.
/// Each entry in <see cref="Settings"/> is inserted if it does not exist, or updated in-place.
/// A null value clears the setting (the row is kept with Value = null, not deleted).
/// </summary>
/// <param name="Settings">Key-value pairs to persist. Keys must be non-empty.</param>
public sealed record UpsertUserSettingsCommand(Dictionary<string, string?> Settings) : IRequest<Result>;
