using DASHBOARD.Application.MyWork.DTOs;
using MediatR;

namespace DASHBOARD.Application.MyWork.Queries.GetMyWork;

/// <summary>Returns every open work item assigned to the current user across all repositories they belong to.</summary>
public sealed record GetMyWorkQuery : IRequest<IReadOnlyList<MyWorkItemDto>>;
