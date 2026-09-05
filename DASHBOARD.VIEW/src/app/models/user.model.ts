/** Matches UserDto returned from GET /api/auth/me. */
export interface UserProfile {
  userId: string;
  email: string;
  name: string;
  avatarClass: string;
  isGlobalAdmin: boolean;
  /** Organization (tenant) the user belongs to. */
  orgId: string;
  /** Organization URL alias. */
  orgAlias: string;
}

/** Minimal user snapshot returned by GET /api/users/search — used by the member-picker. */
export interface UserPickerItem {
  userId: string;
  name: string;
  email: string;
  avatarClass: string;
}
