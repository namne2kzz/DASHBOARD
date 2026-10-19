namespace DASHBOARD.Controllers.Attachments.Requests;

/// <summary>Body for <c>POST sprint-tasks/{taskId}/attachments/upload-url</c>.</summary>
/// <param name="FileName">Original file name.</param>
/// <param name="ContentType">MIME type of the file.</param>
/// <param name="SizeBytes">File size in bytes.</param>
/// <param name="InlineImage">True when the file is a pasted inline image (stricter image rules).</param>
public sealed record RequestAttachmentUploadUrlRequest(
    string FileName,
    string ContentType,
    long   SizeBytes,
    bool   InlineImage = false);
