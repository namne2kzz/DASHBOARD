/** A metadata catalog entry (global or repository-scoped) returned by the API. */
export interface MetadataDto {
  id:           string;
  repositoryId: string | null;
  isGlobal:     boolean;
  key:          string;
  displayName:  string;
  value:        string;
  createdAt:    string;
}

/** A well-known metadata key with its display title, loaded from the API. */
export interface MetadataKeyOption {
  key:         string;
  displayName: string;
}

/** Payload for creating a metadata catalog entry. */
export interface CreateMetadataPayload {
  key:      string;
  value:    string;
  isGlobal: boolean;
}

/** Payload for updating a metadata catalog entry's value. */
export interface UpdateMetadataPayload {
  value: string;
}
