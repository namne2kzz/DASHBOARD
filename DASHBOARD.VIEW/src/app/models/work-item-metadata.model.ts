/** A metadata catalog value assigned to a work item (GET /sprint-tasks/{id}/metadata). */
export interface WorkItemMetadataDto {
  metadataId: string;
  key:        number;
  keyName:    string;
  value:      string;
}
