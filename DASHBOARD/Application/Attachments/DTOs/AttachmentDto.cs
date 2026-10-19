namespace DASHBOARD.Application.Attachments.DTOs;

/// <summary>A work-item attachment shown in the Attachments tab.</summary>
/// <param name="Id">Attachment id.</param>
/// <param name="FileName">Original file name.</param>
/// <param name="ContentType">MIME type.</param>
/// <param name="SizeBytes">File size in bytes.</param>
/// <param name="PublicUrl">Download URL.</param>
/// <param name="UploadedByName">Display name of the uploader.</param>
/// <param name="UploadedByAvatar">Uploader avatar CSS class, if any.</param>
/// <param name="CreatedAt">UTC upload time.</param>
public sealed record AttachmentDto(
    Guid      Id,
    string    FileName,
    string    ContentType,
    long      SizeBytes,
    string    PublicUrl,
    string    UploadedByName,
    string?   UploadedByAvatar,
    DateTime  CreatedAt);
