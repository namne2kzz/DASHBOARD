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
  managerId:        string | null;
  managerName:      string | null;
  repoMemberships:  UserRepoMembership[];
}

/** A lightweight user node in the organisation hierarchy view. */
export interface UserHierarchyNode {
  id:               string;
  name:             string;
  email:            string;
  avatarClass:      string;
  isGlobalAdmin:    boolean;
  isActive:         boolean;
  subordinateCount: number;
}

/** The organisation-chart slice centred on a user (GET /users/{id}/hierarchy). */
export interface UserHierarchy {
  ancestors:    UserHierarchyNode[];
  manager:      UserHierarchyNode | null;
  self:         UserHierarchyNode;
  peers:        UserHierarchyNode[];
  subordinates: UserHierarchyNode[];
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
  managerId:     string | null;
}
