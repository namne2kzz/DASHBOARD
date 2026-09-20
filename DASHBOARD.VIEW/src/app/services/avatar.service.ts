import { inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpEventType, HttpHeaders, HttpRequest } from '@angular/common/http';
import { catchError, filter, map, switchMap, tap, throwError } from 'rxjs';
import { environment } from '../../environments/environment';
import { AVATAR_ACCEPTED_TYPES, AVATAR_MAX_BYTES } from '../core/constants/system.constant';
import {
  AvatarConfirmResponse,
  AvatarUploadState,
  AvatarUploadUrlResponse,
} from '../models/avatar.model';
import { AuthService } from './auth.service';

/**
 * Handles the full avatar upload flow:
 * 1. Validates the file client-side (type + size).
 * 2. Requests a presigned PUT URL from the backend.
 * 3. PUTs the file directly to MinIO (with progress tracking).
 * 4. Confirms the upload to the backend (server verifies existence + persists public URL).
 * 5. Patches the in-memory auth profile so the UI updates immediately without a reload.
 */
@Injectable({ providedIn: 'root' })
export class AvatarService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly api  = `${environment.apiBaseUrl}/users/me/avatar`;

  /** Upload progress state — components bind to this to show a progress bar or error. */
  readonly uploadState = signal<AvatarUploadState>({ progress: 0, done: false, error: null });

  // ── Public API ─────────────────────────────────────────────────────────────

  /**
   * Validates the file then runs the full three-step upload flow.
   * Updates {@link uploadState} throughout so the UI can react without any extra logic.
   * @param file The image file selected by the user.
   */
  uploadAvatar(file: File): void {
    const validationError = this._validate(file);
    if (validationError) {
      this.uploadState.set({ progress: 0, done: false, error: validationError });
      return;
    }

    this.uploadState.set({ progress: 0, done: false, error: null });

    this.http.post<AvatarUploadUrlResponse>(`${this.api}/upload-url`, {}).pipe(
      switchMap(({ uploadUrl, objectKey }) =>
        this._putToMinio(uploadUrl, file).pipe(
          tap(progress => this.uploadState.update(s => ({ ...s, progress }))),
          // Only proceed to confirm once MinIO acknowledges the PUT is complete.
          filter(progress => progress === 100),
          switchMap(() =>
            this.http.patch<AvatarConfirmResponse>(this.api, { objectKey })
          ),
        )
      ),
      catchError(err => {
        const message = err?.error?.error ?? 'Avatar upload failed. Please try again.';
        this.uploadState.set({ progress: 0, done: false, error: message });
        return throwError(() => err);
      }),
    ).subscribe(({ avatarUrl }) => {
      this.auth.patchProfile({ avatarUrl });
      this.uploadState.set({ progress: 100, done: true, error: null });
    });
  }

  /** Resets upload state — call when the user dismisses the upload area or opens a new dialog. */
  resetState(): void {
    this.uploadState.set({ progress: 0, done: false, error: null });
  }

  // ── Internals ──────────────────────────────────────────────────────────────

  /**
   * Validates file type and size before hitting the network.
   * @param file The candidate file.
   * @returns An error message string, or null if the file is acceptable.
   */
  private _validate(file: File): string | null {
    if (!(AVATAR_ACCEPTED_TYPES as readonly string[]).includes(file.type))
      return 'Please select a JPEG, PNG, WebP, or GIF image.';
    if (file.size > AVATAR_MAX_BYTES)
      return 'Image must be smaller than 5 MB.';
    return null;
  }

  /**
   * PUTs the file body directly to MinIO using the presigned URL.
   * Emits upload progress as integers (0–100).
   * The `Content-Type` header must match the MIME type that was signed into the URL.
   * @param presignedUrl Presigned PUT URL returned by the backend.
   * @param file The file to upload.
   */
  private _putToMinio(presignedUrl: string, file: File) {
    const req = new HttpRequest('PUT', presignedUrl, file, {
      headers: new HttpHeaders({ 'Content-Type': file.type }),
      reportProgress: true,
    });

    return this.http.request(req).pipe(
      filter(event =>
        event.type === HttpEventType.UploadProgress ||
        event.type === HttpEventType.Response
      ),
      map(event => {
        if (event.type === HttpEventType.UploadProgress && event.total) {
          return Math.round(100 * event.loaded / event.total);
        }
        return 100;
      }),
    );
  }
}
