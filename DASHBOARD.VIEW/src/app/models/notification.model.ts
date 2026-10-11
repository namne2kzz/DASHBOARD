export type NotificationType =
  | 'WorkItemAssigned'
  | 'WorkItemUnassigned'
  | 'WorkItemStateChanged'
  | 'SprintActivated'
  | 'SprintClosed'
  | 'MentionedInDiscussion'
  | 'AddedToRepository'
  | 'RemovedFromRepository'
  | 'AddedToSprint'
  | 'RemovedFromSprint';

export interface AppNotification {
  id: string;
  type: NotificationType;
  title: string;
  body: string;
  url: string | null;
  isRead: boolean;
  createdAt: string;
}
