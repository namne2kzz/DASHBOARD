namespace DASHBOARD.Application.Users.DTOs;

/// <summary>Minimal user snapshot returned by the member-picker search endpoint.</summary>
public sealed record UserPickerItemDto(
    Guid   UserId,
    string Name,
    string Email,
    string AvatarClass);
