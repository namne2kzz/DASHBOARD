/** Matches MemberDto from the backend. */
export interface MemberApiDto {
  memberId: string;
  userId: string;
  userName: string;
  userEmail: string;
  avatarClass: string;
  defaultRole: string;
  roleId: string;
  roleName: string | null;
  joinedAt: string;
}

/** Matches MentionCandidateDto from the backend — a user who can be @mentioned in a discussion. */
export interface MentionCandidateApiDto {
  userId: string;
  name: string;
  avatarClass: string;
}
