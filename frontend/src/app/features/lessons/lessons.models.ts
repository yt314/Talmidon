export enum LessonStatus {
  Requested = 0,
  Scheduled = 1,
  Completed = 2,
  Cancelled = 3,
  Declined = 4
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

export const LESSON_STATUS_LABELS: Record<LessonStatus, string> = {
  [LessonStatus.Requested]: 'ממתין לאישור',
  [LessonStatus.Scheduled]: 'מתוזמן',
  [LessonStatus.Completed]: 'התקיים',
  [LessonStatus.Cancelled]: 'בוטל',
  [LessonStatus.Declined]: 'נדחה'
};

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
