namespace DASHBOARD.Application.Discussions.DTOs;

/// <summary>A user who can be @mentioned in a sprint task's discussion.</summary>
/// <param name="UserId">The mentionable user's ID.</param>
/// <param name="Name">Display name shown in the picker.</param>
/// <param name="AvatarClass">Tailwind avatar colour class.</param>
public sealed record MentionCandidateDto(
    Guid   UserId,
    string Name,
    string AvatarClass);
