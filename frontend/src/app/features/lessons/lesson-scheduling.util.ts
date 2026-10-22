import { formatDate } from '@angular/common';
import { AbstractControl, ValidationErrors } from '@angular/forms';
import { ConfirmationService } from 'primeng/api';
import { AvailabilityWindow } from '../teacher/profile/profile.models';
import { CalendarEventItem, SaveCalendarEventRequest } from './calendar-events.models';
import { LessonAmountSuggestion } from './lesson-pricing.util';
import { ChangeRequest, ChangeRequestType, Lesson, LessonStatus } from './lessons.models';

/**
 * כללי היומן של המורה, כפונקציות טהורות: התנגשויות, שעות זמינות, מה ממתין לטיפול
 * וטווחי אירועים. מופרדים מהרכיבים כדי שאפשר יהיה לבדוק אותם בלי DOM.
 */

const DAY_MS = 24 * 60 * 60 * 1000;
const HOUR_MS = 60 * 60 * 1000;

const time = (iso: string) => new Date(iso).getTime();

/** משלב תאריך (חלק היום/חודש/שנה) עם שעה (חלק השעה/דקה) לאובייקט Date אחד. */
export function combineDateTime(date: Date, timeOfDay: Date): Date {
  const combined = new Date(date);
  combined.setHours(timeOfDay.getHours(), timeOfDay.getMinutes(), 0, 0);
  return combined;
}

/** ממיר "HH:mm" למספר דקות מתחילת היום. */
export function toMinutes(hhmm: string): number {
  const [h, m] = hhmm.split(':').map(Number);
  return h * 60 + (m || 0);
}

/** מאתר שיעור פעיל (מתוזמן/התקיים/ממתין) שחופף לטווח הנתון — לאזהרת כפל-הזמנה. */
export function findConflict(lessons: readonly Lesson[], start: Date, end: Date, excludeLessonId?: string): Lesson | null {
  return (
    lessons.find(
      l =>
        l.id !== excludeLessonId &&
        (l.status === LessonStatus.Scheduled || l.status === LessonStatus.Completed || l.status === LessonStatus.Requested) &&
        start < new Date(l.endTime) &&
        end > new Date(l.startTime)
    ) ?? null
  );
}

/** ההתנגשות שמוצעת לשינוי מועד שהורה ביקש. בקשת ביטול אינה מתנגשת בדבר. */
export function findChangeRequestConflict(lessons: readonly Lesson[], request: ChangeRequest): Lesson | null {
  if (request.type !== ChangeRequestType.Reschedule || !request.proposedStartTime || !request.proposedEndTime) return null;
  return findConflict(lessons, new Date(request.proposedStartTime), new Date(request.proposedEndTime), request.lessonId);
}

/** The clash itself, worded once and reused by the dialogs and the prompts. */
export function conflictText(conflict: Lesson, locale: string): string {
  const range = `${formatDate(conflict.startTime, 'HH:mm', locale)}–${formatDate(conflict.endTime, 'HH:mm', locale)}`;
  return `כבר קיים שיעור עם ${conflict.studentName} בשעה ${range}`;
}

/** הודעת אזהרה לפני שמירת מועד (התנגשות / מחוץ לשעות), או null אם הכל תקין. */
export function preSaveWarning(
  lessons: readonly Lesson[],
  windows: readonly AvailabilityWindow[],
  start: Date,
  end: Date,
  locale: string,
  excludeLessonId?: string
): string | null {
  const conflict = findConflict(lessons, start, end, excludeLessonId);
  if (conflict) return `${conflictText(conflict, locale)}. לקבוע בכל זאת?`;
  if (isOutsideAvailability(windows, start, end)) return 'השיעור נקבע מחוץ לשעות הזמינות שהגדרת. לקבוע בכל זאת?';
  return null;
}

/** האם הטווח נופל מחוץ לשעות הזמינות שהוגדרו. ללא הגדרות זמינות — תמיד false (אין הגבלה). */
export function isOutsideAvailability(windows: readonly AvailabilityWindow[], start: Date, end: Date): boolean {
  if (windows.length === 0) return false;
  const day = start.getDay();
  const startMin = start.getHours() * 60 + start.getMinutes();
  const endMin = end.getHours() * 60 + end.getMinutes();
  return !windows.some(w => w.dayOfWeek === day && startMin >= toMinutes(w.startTime) && endMin <= toMinutes(w.endTime));
}

/** שיעורים שהמועד שלהם עבר ועדיין "מתוזמן" — ממתינים לסימון (התקיים/בוטל), מהמוקדם. */
export function lessonsAwaitingMark(lessons: readonly Lesson[], now: number): Lesson[] {
  return lessons
    .filter(l => l.status === LessonStatus.Scheduled && time(l.endTime) < now)
    .sort((a, b) => time(a.startTime) - time(b.startTime));
}

/**
 * כמה שיעורים תמחק הסימון "למחוק גם שיעורים עתידיים". בלי המספר הזה זו הייתה
 * פעולה הרסנית בהיקף לא ידוע — שני שיעורים או שלושים, אותה תיבת סימון.
 */
export function futureOccurrencesOfSeries(lessons: readonly Lesson[], seriesId: string, now: number): number {
  return lessons.filter(l => l.seriesId === seriesId && l.status === LessonStatus.Scheduled && time(l.startTime) > now).length;
}

export type PendingAnswer = { kind: 'lesson'; lesson: Lesson } | { kind: 'change'; request: ChangeRequest };

/**
 * הבקשה שהשיעור שלה הכי קרוב, מבין בקשות לשיעור חדש ובקשות שינוי. המיון לפי מועד
 * השיעור ולא לפי מועד הבקשה: בקשה למחר מאבדת את משמעותה לפני בקשה לעוד חודש.
 * בתיקו — בקשה לשיעור חדש קודמת.
 */
export function nextPendingAnswer(lessonRequests: readonly Lesson[], changeRequests: readonly ChangeRequest[]): PendingAnswer | null {
  const lesson = earliest(lessonRequests, l => time(l.startTime));
  const request = earliest(changeRequests, r => time(r.lessonStartTime));
  const lessonAt = lesson ? time(lesson.startTime) : Infinity;
  const changeAt = request ? time(request.lessonStartTime) : Infinity;

  if (lesson && lessonAt <= changeAt) return { kind: 'lesson', lesson };
  return request ? { kind: 'change', request } : null;
}

function earliest<T>(items: readonly T[], at: (item: T) => number): T | undefined {
  return items.slice().sort((a, b) => at(a) - at(b))[0];
}

/** הסבר קצר בעברית לסכום שהוצע, כדי שהמורה תבין מאיפה הוא הגיע. */
export function amountExplanation(suggestion: LessonAmountSuggestion): string {
  const minutes = `${suggestion.durationMinutes} דק׳`;
  switch (suggestion.source) {
    case 'student':
      return `לפי המחיר הקבוע שנקבע לתלמיד/ה (${minutes}).`;
    case 'student-prorated':
      return `המחיר הקבוע של התלמיד/ה, מחושב יחסית ל-${minutes}.`;
    case 'exact':
      return `לפי מחירון השיעורים שלך ל-${minutes}.`;
    case 'prorated':
      return `חושב יחסית למחיר לשעה לפי ${minutes}.`;
    case 'unknown':
      return 'לא הצלחנו לחשב סכום — אפשר להזין אותו ידנית.';
  }
}

// ===== אירועים אישיים =====

export interface EventFormValue {
  title: string;
  date: Date | null;
  endDate: Date | null;
  startTime: Date | null;
  endTime: Date | null;
  isAllDay: boolean;
  notes: string;
}

/**
 * מועד ברירת המחדל לאירוע חדש: השעה העגולה הבאה. היומן מציג 07:00–22:00, ולכן
 * ברירת המחדל נשארת בתוך החלון הזה: אירוע ב-23:00 היה נשמר כשורה שהמורה אינה
 * רואה בשום תצוגה.
 */
export function defaultEventStart(now: Date): Date {
  const start = new Date(now);
  start.setMinutes(0, 0, 0);
  start.setHours(start.getHours() + 1);
  if (start.getHours() < 7 || start.getHours() > 20) {
    if (start.getHours() > 20) start.setDate(start.getDate() + 1);
    start.setHours(9);
  }
  return start;
}

export function newEventFormValue(now: Date): EventFormValue {
  const start = defaultEventStart(now);
  return { title: '', date: start, endDate: null, startTime: start, endTime: new Date(start.getTime() + HOUR_MS), isAllDay: false, notes: '' };
}

/**
 * אירוע קיים בטופס. באירוע של יום שלם השרת מחזיק "סוף בלעדי" (חצות של היום שאחרי),
 * ולכן בתצוגה מחסירים יום אחד; אחרת אירוע של יום אחד היה נראה בטופס כנמשך יומיים.
 */
export function eventToFormValue(item: CalendarEventItem): EventFormValue {
  const start = new Date(item.startTime);
  const end = new Date(item.endTime);
  const lastDay = item.isAllDay ? new Date(end.getTime() - DAY_MS) : end;
  return {
    title: item.title,
    date: start,
    endDate: item.isAllDay && lastDay.toDateString() !== start.toDateString() ? lastDay : null,
    startTime: item.isAllDay ? null : start,
    endTime: item.isAllDay ? null : end,
    isAllDay: item.isAllDay,
    notes: item.notes ?? ''
  };
}

/** הבקשה לשרת מתוך הטופס, או null אם חסרות שעות באירוע שאינו יום שלם. */
export function formValueToEventRequest(raw: EventFormValue): SaveCalendarEventRequest | null {
  const day = raw.date;
  if (!day) return null;

  let start: Date;
  let end: Date;
  if (raw.isAllDay) {
    start = new Date(day.getFullYear(), day.getMonth(), day.getDate());
    const last = raw.endDate && raw.endDate > day ? raw.endDate : day;
    // סוף בלעדי: חצות של היום שאחרי האחרון, כך שיום אחד נמשך בדיוק 24 שעות
    end = new Date(last.getFullYear(), last.getMonth(), last.getDate() + 1);
  } else {
    if (!raw.startTime || !raw.endTime) return null;
    start = combineDateTime(day, raw.startTime);
    end = combineDateTime(day, raw.endTime);
  }

  return {
    title: raw.title.trim(),
    startTime: start.toISOString(),
    endTime: end.toISOString(),
    isAllDay: raw.isAllDay,
    notes: raw.notes.trim() || null
  };
}

// ===== טפסים ודיאלוגים =====

/** כשמסמנים שיעור כהתקיים+נדרש תשלום, יש להזין סכום גדול מאפס. */
export function amountRequiredWhenChargingValidator(group: AbstractControl): ValidationErrors | null {
  const completed = group.get('completed')?.value;
  const paymentRequired = group.get('paymentRequired')?.value;
  const amountControl = group.get('amount');
  if (!amountControl) return null;

  const needsAmount = completed && paymentRequired && (amountControl.value ?? 0) <= 0;
  const currentErrors = amountControl.errors ?? {};
  const hasRequired = !!currentErrors['required'];

  if (needsAmount && !hasRequired) {
    amountControl.setErrors({ ...currentErrors, required: true });
  } else if (!needsAmount && hasRequired) {
    const { required, ...rest } = currentErrors;
    amountControl.setErrors(Object.keys(rest).length > 0 ? rest : null);
  }
  return null;
}

/** "שים לב" לפני פעולה שמותר לבצע בכל זאת — התנגשות, או מחוץ לשעות הזמינות. */
export function confirmAnyway(
  confirmation: ConfirmationService,
  message: string,
  proceed: () => void,
  acceptLabel = 'קבע בכל זאת'
): void {
  confirmation.confirm({
    header: 'שים לב',
    message,
    icon: 'pi pi-exclamation-triangle',
    acceptLabel,
    rejectLabel: 'ביטול',
    accept: proceed
  });
}
