/** טבלת המחירים של המורה, כפי שהיא מגיעה מהפרופיל. */
export interface TeacherPricing {
  /** המחיר לשיעור מלא — שעה. תמיד מוגדר. */
  pricePerHour: number;
  /** מחיר ל-45 דקות, אם המורה הגדירה. */
  pricePer45?: number | null;
  /** מחיר ל-30 דקות, אם המורה הגדירה. */
  pricePer30?: number | null;
}

/** מה שסוכם עם התלמיד/ה: מחיר לשיעור, והמשך שאליו המחיר הזה מתייחס. */
export interface StudentPricing {
  pricePerLesson?: number | null;
  /** משך ברירת המחדל של התלמיד/ה — המשך שהמחיר שלמעלה נקבע עבורו. */
  durationMinutes?: number | null;
}

/** מאיפה הגיע הסכום המוצע — כדי שנוכל להסביר אותו למורה במסך. */
export type LessonPriceSource =
  /** המחיר הקבוע שסוכם עם התלמיד/ה, לשיעור באורך הרגיל שלו */
  | 'student'
  /** אותו מחיר קבוע, אבל השיעור היה ארוך או קצר מהרגיל — חושב יחסית */
  | 'student-prorated'
  /** ערך מפורש בטבלת המחירים של המורה (60 / 45 / 30 דקות) */
  | 'exact'
  /** חושב יחסית למחיר לשעה של המורה */
  | 'prorated'
  /** אין ממה לחשב (אין מחיר, או שמשך השיעור אינו תקין) */
  | 'unknown';

export interface LessonAmountSuggestion {
  amount: number;
  source: LessonPriceSource;
  durationMinutes: number;
}

/**
 * כמה לגבות על שיעור שהסתיים.
 *
 * סדר הכרעה, מהמחייב לכללי:
 *
 * 1. **מה שסוכם עם התלמיד/ה** גובר על הכול. אם הוסכם 120 ₪ לשיעור, זה המחיר —
 *    גם אם בטבלה של המורה כתוב אחרת.
 * 2. **שיעור באורך חריג אצל אותו תלמיד/ה** — המחיר המוסכם מחושב יחסית למשך הרגיל
 *    שלו. מי שמשלם 120 ₪ על 45 דקות ישלם 240 ₪ על שיעור כפול, ולא 120.
 * 3. **התאמה מדויקת בטבלת המורה** — 60, 45 או 30 דקות. תמחור של מורים אינו לינארי:
 *    מי שגובה 140 ₪ לשעה גובה לרוב 120 ₪ ל-45 דקות ולא 105, ולכן ערך מוגדר תמיד
 *    עדיף על חישוב.
 * 4. **חישוב יחסי מהמחיר לשעה** לכל משך אחר (למשל 90 דקות).
 *
 * כל חישוב יחסי מעוגל ל-5 ₪ הקרובים, כדי לא לייצר סכומים כמו 157.5 ₪.
 * המורה תמיד יכולה לדרוס את התוצאה בשדה הסכום — זו הצעה, לא החלטה.
 */
export function suggestLessonAmountDetailed(
  durationMinutes: number,
  pricing: TeacherPricing,
  student?: StudentPricing | null
): LessonAmountSuggestion {
  const studentPrice = student?.pricePerLesson;

  if (studentPrice != null) {
    const studentDuration = student?.durationMinutes;
    if (!(studentDuration != null && studentDuration > 0) || studentDuration === durationMinutes) {
      return { amount: studentPrice, source: 'student', durationMinutes };
    }
    if (durationMinutes > 0) {
      return {
        amount: roundToFive((studentPrice * durationMinutes) / studentDuration),
        source: 'student-prorated',
        durationMinutes
      };
    }
  }

  const exact = exactMatch(durationMinutes, pricing);
  if (exact != null) {
    return { amount: exact, source: 'exact', durationMinutes };
  }

  if (!(durationMinutes > 0) || !(pricing.pricePerHour > 0)) {
    return { amount: 0, source: 'unknown', durationMinutes };
  }

  return {
    amount: roundToFive((pricing.pricePerHour * durationMinutes) / 60),
    source: 'prorated',
    durationMinutes
  };
}

/** רק הסכום, בלי הנימוק. */
export function suggestLessonAmount(
  durationMinutes: number,
  pricing: TeacherPricing,
  student?: StudentPricing | null
): number {
  return suggestLessonAmountDetailed(durationMinutes, pricing, student).amount;
}

function exactMatch(minutes: number, pricing: TeacherPricing): number | null {
  if (minutes === 60) return pricing.pricePerHour;
  if (minutes === 45 && pricing.pricePer45 != null) return pricing.pricePer45;
  if (minutes === 30 && pricing.pricePer30 != null) return pricing.pricePer30;
  return null;
}

/** 157.5 ₪ הוא סכום שאיש אינו גובה. */
function roundToFive(value: number): number {
  return Math.round(value / 5) * 5;
}

/** משך השיעור בדקות, מתוך הזמנים שלו. */
export function lessonDurationMinutes(startTime: string, endTime: string): number {
  return Math.round((new Date(endTime).getTime() - new Date(startTime).getTime()) / 60000);
}
