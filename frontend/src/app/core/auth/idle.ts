/**
 * מתי חשבון שנשאר פתוח בלי נגיעה נחשב נטוש.
 *
 * שלושים דקות הן ברירת המחדל המקובלת במערכות עסקיות שמחזיקות מידע אישי — קצר מספיק
 * כדי שמסך שנשאר פתוח בחדר מורים לא יישאר פתוח, וארוך מספיק כדי לא לנעול מישהי
 * שקראה הערה ארוכה או ענתה לטלפון באמצע.
 *
 * ההחלטה היא פונקציה של חותמות זמן ולא של טיימר: מחשב שנכנס לשינה מקפיא טיימרים,
 * ואז ההתנתקות פשוט לא הייתה קורית — דווקא במצב שבו היא הכי נחוצה.
 */
export const IDLE_LIMIT_MS = 30 * 60_000;

/** האזהרה מופיעה דקה לפני. מספיק זמן להגיב, קצר מספיק שלא תישכח על המסך. */
export const IDLE_WARNING_MS = 60_000;

export type IdleState = 'active' | 'warning' | 'expired';

export function idleState(lastActivityMs: number, nowMs: number): IdleState {
  const idleFor = nowMs - lastActivityMs;

  if (idleFor >= IDLE_LIMIT_MS) return 'expired';
  if (idleFor >= IDLE_LIMIT_MS - IDLE_WARNING_MS) return 'warning';
  return 'active';
}

/** כמה שניות נשארו עד ההתנתקות, לתצוגה באזהרה. */
export function secondsUntilLogout(lastActivityMs: number, nowMs: number): number {
  return Math.max(0, Math.ceil((lastActivityMs + IDLE_LIMIT_MS - nowMs) / 1000));
}
