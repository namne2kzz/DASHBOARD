using DASHBOARD.Application.SystemInfo.DTOs;
using MediatR;

namespace DASHBOARD.Application.SystemInfo.Queries.GetAbout;

/// <summary>Returns build information for this API and the live status of every service it depends on.</summary>
public sealed record GetAboutQuery : IRequest<AboutDto>;
