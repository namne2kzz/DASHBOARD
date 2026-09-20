using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Users.Commands.UpsertUserSettings;

/// <summary>
/// Handles <see cref="UpsertUserSettingsCommand"/>: loads existing rows for the caller,
/// updates matching keys in-place, and inserts rows for new keys — all in one round-trip.
/// </summary>
public sealed class UpsertUserSettingsCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<UpsertUserSettingsCommand, Result>
{
    /// <summary>Upserts preference settings for the current user and commits.</summary>
    /// <param name="command">Key-value pairs to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success; <see cref="Result.Failure"/> on validation error.</returns>
    public async Task<Result> Handle(UpsertUserSettingsCommand command, CancellationToken ct)
    {
        if (command.Settings is not { Count: > 0 })
            return Result.Failure("Settings payload must contain at least one entry.");

        // Reject any blank or whitespace-only key.
        if (command.Settings.Keys.Any(k => string.IsNullOrWhiteSpace(k)))
            return Result.Failure("Setting keys must not be empty.");

        var keysToUpsert = command.Settings.Keys.ToList();

        // Load all existing rows for this user that match the incoming keys in one query.
        var existing = await db.Set<UserSetting>()
            .AsTracking()
            .Where(s => s.UserId == user.UserId && keysToUpsert.Contains(s.Key))
            .ToListAsync(ct);

        var existingMap = existing.ToDictionary(s => s.Key);

        foreach (var (key, value) in command.Settings)
        {
            if (existingMap.TryGetValue(key, out var row))
            {
                // Update existing row only when the value actually changed.
                if (row.Value != value)
                    row.SetValue(value);
            }
            else
            {
                // Insert new row.
                db.Set<UserSetting>().Add(UserSetting.Create(user.UserId, key, value));
            }
        }

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
