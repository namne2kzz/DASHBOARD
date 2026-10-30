/** Matches UserDto returned from GET /api/auth/me. */
export interface UserProfile {
  userId: string;
  email: string;
  name: string;
  avatarClass: string;
  /** Public URL of the uploaded avatar image. Null/absent = use avatarClass colour chip instead. */
  avatarUrl?: string | null;
  isGlobalAdmin: boolean;
  /** Organization (tenant) the user belongs to. */
  orgId: string;
  /** Organization URL alias — the tenant segment in every app URL. */
  orgAlias: string;
  /** Organization display name, for labelling the tenant in the UI. */
  orgName: string;
}

/** Minimal user snapshot returned by GET /api/users/search — used by the member-picker. */
export interface UserPickerItem {
  userId: string;
  name: string;
  email: string;
  avatarClass: string;
}
