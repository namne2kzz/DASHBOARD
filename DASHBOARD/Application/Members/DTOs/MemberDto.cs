using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Application.Members.DTOs;

/// <summary>Repository member snapshot including their user profile and assigned role.</summary>
public sealed record MemberDto(
    Guid      MemberId,
    Guid      UserId,
    string    UserName,
    string    UserEmail,
    string    AvatarClass,
    string    DefaultRole,
    Guid?     RoleId,
    string?   RoleName,
    DateTime  JoinedAt);
