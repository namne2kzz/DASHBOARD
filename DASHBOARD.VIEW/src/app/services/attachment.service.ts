import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpRequest, HttpEventType } from '@angular/common/http';
import { filter, firstValueFrom, lastValueFrom, map } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AttachmentDto,
  AttachmentUploadUrlResponse,
  ATTACHMENT_MAX_BYTES,
  INLINE_IMAGE_TYPES,
  INLINE_IMAGE_MAX_BYTES,
} from '../models/attachment.model';

const API = `${environment.apiBaseUrl}/repositories`;

/** Handles uploading files and pasted inline images for a work item via presigned MinIO URLs. */
@Injectable({ providedIn: 'root' })
export class AttachmentService {
  private readonly http = inject(HttpClient);

  private base(repoId: string, taskId: string): string {
    return `${API}/${repoId}/sprint-tasks/${taskId}/attachments`;
  }

  /** Lists a work item's attachments. @param repoId Repository ID. @param taskId Work item ID. */
  list(repoId: string, taskId: string): Promise<AttachmentDto[]> {
    return firstValueFrom(this.http.get<AttachmentDto[]>(this.base(repoId, taskId)));
  }

  /**
   * Uploads a general file attachment: presigned PUT then confirm.
   * @param repoId Repository ID. @param taskId Work item ID. @param file The file.
   * @returns The created attachment record.
   * @throws Error when the file exceeds the size limit.
   */
  async upload(repoId: string, taskId: string, file: File): Promise<AttachmentDto> {
    if (file.size > ATTACHMENT_MAX_BYTES)
      throw new Error('File must be smaller than 50 MB.');

    const target = await firstValueFrom(
      this.http.post<AttachmentUploadUrlResponse>(`${this.base(repoId, taskId)}/upload-url`, {
        fileName: file.name, contentType: file.type || 'application/octet-stream',
        sizeBytes: file.size, inlineImage: false,
      }),
    );

    await this._putToStorage(target.uploadUrl, file);

    return firstValueFrom(
      this.http.post<AttachmentDto>(this.base(repoId, taskId), {
        fileName: file.name, contentType: file.type || 'application/octet-stream',
        sizeBytes: file.size, objectKey: target.objectKey,
      }),
    );
  }

  /** Deletes an attachment. @param repoId Repository ID. @param taskId Work item ID. @param attachmentId Attachment ID. */
  delete(repoId: string, taskId: string, attachmentId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(`${this.base(repoId, taskId)}/${attachmentId}`),
    );
  }

  /**
   * Uploads a pasted/dropped image and returns its public URL for embedding in markdown.
   * @param repoId Repository ID.
   * @param taskId Work item ID.
   * @param file The image file.
   * @returns The public URL of the uploaded image.
   * @throws Error when the file is not an accepted image or exceeds the size limit.
   */
  async uploadInlineImage(repoId: string, taskId: string, file: File): Promise<string> {
    if (!(INLINE_IMAGE_TYPES as readonly string[]).includes(file.type))
      throw new Error('Only PNG, JPEG, GIF or WebP images can be pasted.');
    if (file.size > INLINE_IMAGE_MAX_BYTES)
      throw new Error('Image must be smaller than 10 MB.');

    const target = await firstValueFrom(
      this.http.post<AttachmentUploadUrlResponse>(
        `${this.base(repoId, taskId)}/upload-url`,
        { fileName: file.name, contentType: file.type, sizeBytes: file.size, inlineImage: true },
      ),
    );

    await this._putToStorage(target.uploadUrl, file);
    return target.publicUrl;
  }

  /** PUTs the file body directly to object storage using the presigned URL. */
  private async _putToStorage(presignedUrl: string, file: File): Promise<void> {
    const req = new HttpRequest('PUT', presignedUrl, file, {
      headers: new HttpHeaders({ 'Content-Type': file.type }),
    });
    await lastValueFrom(
      this.http.request(req).pipe(
        filter(e => e.type === HttpEventType.Response),
        map(() => void 0),
      ),
    );
  }
}
