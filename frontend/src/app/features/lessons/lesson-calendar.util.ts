import { EventInput } from 'fullcalendar';
import { CalendarEventExtendedProps } from '../../shared/calendar/lesson-calendar.model';
import { ChangeRequest, ChangeRequestType, LESSON_STATUS_COLOR, Lesson, LessonStatus, PENDING_CHANGE_COLOR } from './lessons.models';

export function lessonToCalendarEvent(lesson: Lesson): EventInput {
  const pending = lesson.status === LessonStatus.Requested;
  const color = LESSON_STATUS_COLOR[lesson.status];
  const extendedProps: CalendarEventExtendedProps = { kind: pending ? 'request' : 'lesson', refId: lesson.id };
  return {
    id: `lesson-${lesson.id}`,
    title: pending ? `⏳ ${lesson.studentName}` : lesson.studentName,
    start: lesson.startTime,
    end: lesson.endTime,
    color: color.color,
    contrastColor: color.contrastColor,
    classNames: pending ? ['lesson-cal-pending'] : lesson.status === LessonStatus.Cancelled ? ['lesson-cal-muted'] : [],
    // רק שיעור מתוזמן ניתן לגרירה למועד אחר; אף פעם לא לשינוי משך (גרירת הקצה).
    startEditable: lesson.status === LessonStatus.Scheduled,
    durationEditable: false,
    extendedProps
  };
}

/**
 * בונה את אירועי היומן של המורה: שיעורים רגילים, ובקשות ממתינות (שיעור חדש/שינוי מועד/ביטול) בעיצוב "ממתין
 * לאישור" (מקווקו). בקשת ביטול מוצגת על גבי בלוק השיעור המקורי; בקשת שינוי מועד מוצגת כבלוק-רפאים נוסף במועד
 * המוצע, לצד השיעור המקורי שנשאר במקומו עד לאישור.
 */
export function buildTeacherCalendarEvents(lessons: Lesson[], pendingChangeRequests: ChangeRequest[]): EventInput[] {
  const cancelRequestByLessonId = new Map(
    pendingChangeRequests.filter(r => r.type === ChangeRequestType.Cancel).map(r => [r.lessonId, r] as const)
  );
  const rescheduleRequests = pendingChangeRequests.filter(
    r => r.type === ChangeRequestType.Reschedule && r.proposedStartTime && r.proposedEndTime
  );

  const lessonEvents: EventInput[] = lessons.map(lesson => {
    const cancelRequest = lesson.status === LessonStatus.Scheduled ? cancelRequestByLessonId.get(lesson.id) : undefined;
    if (!cancelRequest) return lessonToCalendarEvent(lesson);

    const extendedProps: CalendarEventExtendedProps = { kind: 'change-cancel', refId: cancelRequest.id };
    return {
      id: `change-cancel-${cancelRequest.id}`,
      title: `⏳ בקשת ביטול — ${lesson.studentName}`,
      start: lesson.startTime,
      end: lesson.endTime,
      color: PENDING_CHANGE_COLOR.color,
      contrastColor: PENDING_CHANGE_COLOR.contrastColor,
      classNames: ['lesson-cal-pending'],
      startEditable: false,
      durationEditable: false,
      extendedProps
    };
  });

  const rescheduleGhosts: EventInput[] = rescheduleRequests.map(request => {
    const extendedProps: CalendarEventExtendedProps = { kind: 'change-reschedule', refId: request.id };
    return {
      id: `change-reschedule-${request.id}`,
      title: `⏳ בקשת שינוי מועד — ${request.studentName}`,
      start: request.proposedStartTime!,
      end: request.proposedEndTime!,
      color: PENDING_CHANGE_COLOR.color,
      contrastColor: PENDING_CHANGE_COLOR.contrastColor,
      classNames: ['lesson-cal-pending'],
      startEditable: false,
      durationEditable: false,
      extendedProps
    };
  });

  return [...lessonEvents, ...rescheduleGhosts];
}
