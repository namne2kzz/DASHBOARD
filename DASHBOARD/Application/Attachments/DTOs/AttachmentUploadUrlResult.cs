namespace DASHBOARD.Application.Attachments.DTOs;

/// <summary>Presigned upload target for a work-item attachment or inline image.</summary>
/// <param name="UploadUrl">Presigned PUT URL the browser uploads the file to directly.</param>
/// <param name="ObjectKey">The storage object key the server assigned.</param>
/// <param name="PublicUrl">The public URL the file will be served from after upload.</param>
public sealed record AttachmentUploadUrlResult(
    string UploadUrl,
    string ObjectKey,
    string PublicUrl);
