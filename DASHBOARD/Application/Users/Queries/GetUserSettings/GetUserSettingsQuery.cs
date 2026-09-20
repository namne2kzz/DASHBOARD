using MediatR;

namespace DASHBOARD.Application.Users.Queries.GetUserSettings;

/// <summary>
/// Returns all persisted preference settings for the currently authenticated user
/// as a key → value dictionary. Missing keys should be treated as unset by the client.
/// </summary>
public sealed record GetUserSettingsQuery : IRequest<Dictionary<string, string?>>;
