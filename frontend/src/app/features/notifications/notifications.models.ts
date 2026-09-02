export enum NotificationType {
  LessonRequest = 0,
  ChangeRequest = 1,
  General = 2,
  ContactRequest = 3
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
  [NotificationType.General]: 'pi pi-info-circle',
  [NotificationType.ContactRequest]: 'pi pi-inbox'
};

/**
 * סוג שאינו מוכר לגרסת הלקוח הזו — למשל אחרי שנוסף סוג בשרת והדפדפן מריץ עדיין
 * גרסה קודמת. בלי הנפילה הזו ‎NOTIFICATION_ICON[type]‎ היה מחזיר undefined
 * ומייצר ‎class="undefined"‎ בלי אייקון.
 */
export function notificationIcon(type: NotificationType): string {
  return NOTIFICATION_ICON[type] ?? 'pi pi-bell';
}
