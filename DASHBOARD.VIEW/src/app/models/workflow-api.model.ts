/** API DTO for a workflow board column returned by GET /board/columns. */
export interface WorkflowColumnApiDto {
  id: string;
  repositoryId: string;
  name: string;
  /** WorkItemState: 0=Open, 1=ToDo, 2=InProgress, 3=InReview, 4=Verified, 5=Running, 6=Done, 7=Passed, 8=Failed, 9=Closed */
  mappedState: number;
  wipLimit: number;
  /** WipMode: 0=Soft, 1=Hard */
  wipMode: number;
  agingLimitDays: number;
  order: number;
}
