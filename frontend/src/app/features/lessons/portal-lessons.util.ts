import { LessonStatus } from './lessons.models';

/**
 * מה ששני הפורטלים — של ההורה ושל התלמידה — צריכים כדי לסנן שיעור. הם מקבלים
 * טיפוסים שונים מהשרת, אבל שואלים עליהם בדיוק את אותן שתי שאלות.
 */
export interface PortalLesson {
  startTime: string;
  status: LessonStatus;
}

/** לוח קצר במסך הראשי, לא היומן המלא. */
const UPCOMING_LIMIT = 5;

/**
 * השיעורים הקרובים, כולל בקשה שממתינה לאישור: תלמידה או הורה שביקשו מועד ועדיין
 * לא נענו אינם אמורים לקרוא "אין שיעורים קרובים" על בקשה ששלחו בעצמם. התגית
 * לצד השורה היא שמבדילה בין "מתוזמן" ל"ממתין לאישור".
 */
export function upcomingPortalLessons<T extends PortalLesson>(lessons: T[], now: Date = new Date()): T[] {
  return lessons
    .filter(
      l =>
        (l.status === LessonStatus.Scheduled || l.status === LessonStatus.Requested) &&
        new Date(l.startTime) >= now
    )
    .sort(byStartTime)
    .slice(0, UPCOMING_LIMIT);
}

/**
 * בקשות שהמורה דחתה והמועד שלהן עוד לא עבר.
 *
 * דחייה מורידה את השיעור מכל רשימות "השיעורים הקרובים", וזה נכון — הוא לא יתקיים.
 * אבל בלי המקום הזה זו הייתה היעלמות שקטה: מי שביקשה ראתה בקשה ממתינה, ואז כלום,
 * בלי לדעת אם נדחתה או שהבקשה לא נקלטה. אחרי שהמועד עבר אין יותר מה לעשות איתה,
 * ולכן היא יורדת מעצמה.
 */
export function declinedPortalRequests<T extends PortalLesson>(lessons: T[], now: Date = new Date()): T[] {
  return lessons
    .filter(l => l.status === LessonStatus.Declined && new Date(l.startTime) >= now)
    .sort(byStartTime)
    .slice(0, UPCOMING_LIMIT);
}

function byStartTime(a: PortalLesson, b: PortalLesson): number {
  return new Date(a.startTime).getTime() - new Date(b.startTime).getTime();
}
