/** Mirrors the backend InvitationStatus enum (System.Text.Json serializes C# enums as numbers by default). */
export enum InvitationStatus {
  Pending = 0,
  Accepted = 1,
  Expired = 2,
  Revoked = 3,
}

/** Matches InvitationDto from the backend. */
export interface InvitationApiDto {
  id: string;
  email: string;
  repositoryId: string;
  status: InvitationStatus;
  expiresAt: string;
}

/** Matches InvitationListItemDto from the backend. */
export interface InvitationListItemDto {
  id: string;
  email: string;
  status: InvitationStatus;
  defaultRole: string;
  roleName: string | null;
  invitedByName: string;
  createdAt: string;
  expiresAt: string;
  acceptedAt: string | null;
}
