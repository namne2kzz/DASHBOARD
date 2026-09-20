/** Progress state during an avatar upload. */
export interface AvatarUploadState {
  /** 0–100 while uploading to MinIO; 100 when confirm step completes. */
  progress: number;
  /** True once the confirm step has succeeded and avatarUrl is persisted in the backend. */
  done: boolean;
  /** Non-null when an error occurred at any stage of the upload flow. */
  error: string | null;
}

/** Response from POST /api/users/me/avatar/upload-url */
export interface AvatarUploadUrlResponse {
  uploadUrl:  string;
  objectKey:  string;
}

/** Response from PATCH /api/users/me/avatar */
export interface AvatarConfirmResponse {
  avatarUrl: string;
}
