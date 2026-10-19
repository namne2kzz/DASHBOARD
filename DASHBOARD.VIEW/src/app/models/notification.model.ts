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

/** Category filter for the notification panel. */
export type NotificationCategory = 'workitems' | 'sprints' | 'membership' | 'mentions';

/** A page of notifications returned by the list endpoint. */
export interface NotificationPage {
  items: AppNotification[];
  hasMore: boolean;
  unreadCount: number;
}
