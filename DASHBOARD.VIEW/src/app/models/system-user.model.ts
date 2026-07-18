/** Mirrors the backend AuthProvider enum. */
export enum AuthProvider {
  System = 0,
  Google = 1,
}

/** Full user record returned by the system-admin endpoint. */
export interface SystemUserDto {
  userId:           string;
  email:            string;
  name:             string;
  avatarClass:      string;
  isGlobalAdmin:    boolean;
  isActive:         boolean;
  authProvider:     AuthProvider;
  createdAt:        string;
  lastLoginAt:      string | null;
  repoMemberships:  UserRepoMembership[];
}

/** A single repository membership entry nested inside a SystemUserDto. */
export interface UserRepoMembership {
  repoId:         string;
  repoName:       string;
  repoCode:       string;
  defaultRole:    string;
  roleName:       string | null;
  joinedAt:       string;
}

/** Payload for creating a new user account. */
export interface CreateUserPayload {
  name:          string;
  email:         string;
  password:      string;
  isGlobalAdmin: boolean;
  avatarClass:   string;
}
