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
