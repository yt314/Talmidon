export enum NotificationType {
  LessonRequest = 0,
  ChangeRequest = 1,
  General = 2
}

export interface AppNotification {
  id: string;
  type: NotificationType;
  title: string;
  message: string;
  linkPath: string | null;
  isRead: boolean;
  createdAt: string;
}

export const NOTIFICATION_ICON: Record<NotificationType, string> = {
  [NotificationType.LessonRequest]: 'pi pi-calendar-plus',
  [NotificationType.ChangeRequest]: 'pi pi-clock',
  [NotificationType.General]: 'pi pi-info-circle'
};
