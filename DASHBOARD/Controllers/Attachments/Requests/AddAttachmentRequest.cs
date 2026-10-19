namespace DASHBOARD.Controllers.Attachments.Requests;

/// <summary>Body for <c>POST sprint-tasks/{taskId}/attachments</c> — confirms an uploaded file.</summary>
/// <param name="FileName">Original file name.</param>
/// <param name="ContentType">MIME type.</param>
/// <param name="SizeBytes">File size in bytes.</param>
/// <param name="ObjectKey">Storage object key from the upload-url step.</param>
public sealed record AddAttachmentRequest(
    string FileName,
    string ContentType,
    long   SizeBytes,
    string ObjectKey);
