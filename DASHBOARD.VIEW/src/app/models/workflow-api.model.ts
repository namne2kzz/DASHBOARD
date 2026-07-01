/** API DTO for a workflow board column returned by GET /board/columns. */
export interface WorkflowColumnApiDto {
  id: string;
  repositoryId: string;
  name: string;
  /** SprintTaskState: 0=New, 1=Backlog, 2=Todo, 3=Active, 4=InReview, 5=Done */
  mappedState: number;
  wipLimit: number;
  /** WipMode: 0=Soft, 1=Hard */
  wipMode: number;
  agingLimitDays: number;
  order: number;
}
