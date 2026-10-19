/** Response from POST sprint-tasks/{taskId}/attachments/upload-url. */
export interface AttachmentUploadUrlResponse {
  uploadUrl: string;
  objectKey: string;
  publicUrl: string;
}

/** A work-item attachment as returned by the list endpoint. */
export interface AttachmentDto {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  publicUrl: string;
  uploadedByName: string;
  uploadedByAvatar: string | null;
  createdAt: string;
}

/** Accepted inline-image MIME types (must match backend AttachmentPolicy.ImageContentTypes). */
export const INLINE_IMAGE_TYPES = ['image/png', 'image/jpeg', 'image/gif', 'image/webp'] as const;

/** Max inline image size — 10 MB (must match backend AttachmentPolicy.MaxImageBytes). */
export const INLINE_IMAGE_MAX_BYTES = 10 * 1024 * 1024;

/** Max attachment size — 50 MB (must match backend AttachmentPolicy.MaxAttachmentBytes). */
export const ATTACHMENT_MAX_BYTES = 50 * 1024 * 1024;
