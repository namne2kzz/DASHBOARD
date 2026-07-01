import { Permission } from '../core/enums/system.enum';

/** Role definition (global default or repository-scoped custom) returned by the API. */
export interface RoleDto {
  id:           string;
  repositoryId: string | null;
  isDefault:    boolean;
  name:         string;
  description:  string | null;
  permissions:  Permission[];
  memberCount:  number;
}

/** Payload for creating a new custom role. */
export interface CreateRolePayload {
  name:        string;
  description: string | null;
  permissions: Permission[];
}

/** Payload for updating an existing custom role. */
export interface UpdateRolePayload {
  name:        string;
  description: string | null;
  permissions: Permission[];
}

/** Payload for adding an existing user directly to the repository. */
export interface AddMemberPayload {
  emailOrUsername: string;
  defaultRole:     string;
}

/** Payload for inviting a new user by email. */
export interface InviteMemberPayload {
  email:        string;
  defaultRole:  string;
  roleId:       string | null;
}

/** A pending invitation record. */
export interface InvitationDto {
  id:        string;
  email:     string;
  role:      string;
  invitedAt: string;
  expiresAt: string;
  status:    'pending' | 'accepted' | 'expired';
}
