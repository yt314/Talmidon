export enum LessonStatus {
  Requested = 0,
  Scheduled = 1,
  Completed = 2,
  Cancelled = 3,
  Declined = 4,
  NoShow = 5
}

export enum LessonOrigin {
  Teacher = 0,
  Parent = 1
}

export enum ChangeRequestType {
  Cancel = 0,
  Reschedule = 1
}

export enum ChangeRequestStatus {
  Pending = 0,
  Approved = 1,
  Rejected = 2
}

export enum LessonSeriesEndCondition {
  Count = 0,
  EndDate = 1,
  Indefinite = 2
}

export const LESSON_STATUS_LABELS: Record<LessonStatus, string> = {
  [LessonStatus.Requested]: 'ממתין לאישור',
  [LessonStatus.Scheduled]: 'מתוזמן',
  [LessonStatus.Completed]: 'התקיים',
  [LessonStatus.Cancelled]: 'בוטל',
  [LessonStatus.Declined]: 'נדחה',
  [LessonStatus.NoShow]: 'לא הגיע'
};

export type LessonStatusSeverity = 'success' | 'info' | 'warn' | 'danger' | 'secondary';

export const LESSON_STATUS_SEVERITY: Record<LessonStatus, LessonStatusSeverity> = {
  [LessonStatus.Requested]: 'warn',
  [LessonStatus.Scheduled]: 'info',
  [LessonStatus.Completed]: 'success',
  [LessonStatus.Cancelled]: 'danger',
  [LessonStatus.Declined]: 'danger',
  [LessonStatus.NoShow]: 'secondary'
};

/** מחלקת CSS לכל סטטוס — משמשת את נקודות המקרא, שמצוירות ב-CSS רגיל. */
export const LESSON_STATUS_CLASS: Record<LessonStatus, string> = {
  [LessonStatus.Requested]: 'lesson-cal-requested',
  [LessonStatus.Scheduled]: 'lesson-cal-scheduled',
  [LessonStatus.Completed]: 'lesson-cal-completed',
  [LessonStatus.Cancelled]: 'lesson-cal-cancelled',
  [LessonStatus.Declined]: 'lesson-cal-declined',
  [LessonStatus.NoShow]: 'lesson-cal-noshow'
};

/** בלוקי-רפאים (ghost) של בקשות שינוי/ביטול ממתינות — אותו עיצוב "ממתין לאישור". */
export const PENDING_CHANGE_CLASS = LESSON_STATUS_CLASS[LessonStatus.Requested];

export interface Lesson {
  id: string;
  studentId: string;
  studentName: string;
  startTime: string;
  endTime: string;
  status: LessonStatus;
  origin: LessonOrigin;
  homework: string | null;
  paymentRequired: boolean;
  amount: number;
  isPaid: boolean;
  completedAt: string | null;
  seriesId: string | null;
}

export interface CreateLessonRequest {
  studentId: string;
  startTime: string;
  endTime: string;
  reason?: string | null;
}

export interface UpdateLessonRequest {
  startTime: string;
  endTime: string;
}

export interface CreateLessonSeriesRequest {
  studentId: string;
  firstStartTime: string;
  firstEndTime: string;
  endCondition: LessonSeriesEndCondition;
  occurrenceCount?: number | null;
  endDate?: string | null;
}

export interface LessonSeriesResult {
  id: string;
  occurrencesCreated: number;
}

export interface CompleteLessonRequest {
  completed: boolean;
  paymentRequired: boolean;
  amount: number;
  homework?: string | null;
  noteContent?: string | null;
  noteVisibleToStudent: boolean;
  noteVisibleToParent: boolean;
}

export interface ChangeRequest {
  id: string;
  lessonId: string;
  studentId: string;
  studentName: string;
  parentName: string;
  type: ChangeRequestType;
  lessonStartTime: string;
  lessonEndTime: string;
  proposedStartTime: string | null;
  proposedEndTime: string | null;
  reason: string | null;
  status: ChangeRequestStatus;
  createdAt: string;
}
