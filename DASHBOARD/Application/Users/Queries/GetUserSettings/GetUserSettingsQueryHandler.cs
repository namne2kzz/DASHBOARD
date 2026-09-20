using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Users.Queries.GetUserSettings;

/// <summary>Handles <see cref="GetUserSettingsQuery"/>: loads all preference rows for the current user.</summary>
public sealed class GetUserSettingsQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<GetUserSettingsQuery, Dictionary<string, string?>>
{
    /// <summary>Fetches every <see cref="UserSetting"/> row for the caller and returns them as a flat dictionary.</summary>
    /// <param name="query">The query (no parameters — caller identity comes from <paramref name="user"/>).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Key-value map of all stored settings; missing keys mean "use app default".</returns>
    public async Task<Dictionary<string, string?>> Handle(GetUserSettingsQuery query, CancellationToken ct)
    {
        var rows = await db.Set<UserSetting>()
            .AsNoTracking()
            .Where(s => s.UserId == user.UserId)
            .Select(s => new { s.Key, s.Value })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Key, r => r.Value);
    }
}
