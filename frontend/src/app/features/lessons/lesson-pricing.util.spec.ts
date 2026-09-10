import { describe, expect, it } from 'vitest';
import { lessonDurationMinutes, suggestLessonAmount, suggestLessonAmountDetailed } from './lesson-pricing.util';

/** מורה טיפוסית: 140 ₪ לשעה, 120 ל-45 דקות, 90 לחצי שעה. */
const pricing = { pricePerHour: 140, pricePer45: 120, pricePer30: 90 };

describe('suggestLessonAmount', () => {
  it('מעדיף את המחיר שסוכם עם התלמיד/ה על פני המחירון של המורה', () => {
    expect(suggestLessonAmount(45, pricing, { pricePerLesson: 100, durationMinutes: 45 })).toBe(100);
  });

  it('מחשב יחסית למחיר של התלמיד/ה כששיעור חורג מהאורך הרגיל שלו', () => {
    // 120 ₪ על 45 דקות → שיעור כפול עולה 240, לא 120
    expect(suggestLessonAmount(90, pricing, { pricePerLesson: 120, durationMinutes: 45 })).toBe(240);
  });

  it('משתמש במחיר של התלמיד/ה כמות שהוא כשאין לו משך ברירת מחדל', () => {
    expect(suggestLessonAmount(90, pricing, { pricePerLesson: 120 })).toBe(120);
  });

  it('לוקח את המחיר ל-45 דקות מהמחירון ולא מחשב אותו יחסית', () => {
    // 140 לשעה היה נותן 105 — אבל המורה הגדירה 120, וזה מה שקובע
    expect(suggestLessonAmount(45, pricing)).toBe(120);
  });

  it('לוקח את המחיר ל-30 דקות מהמחירון', () => {
    expect(suggestLessonAmount(30, pricing)).toBe(90);
  });

  it('מחשב יחסית למחיר לשעה כשאין ערך מתאים במחירון', () => {
    expect(suggestLessonAmount(90, pricing)).toBe(210);
  });

  it('מעגל חישוב יחסי ל-5 ₪ הקרובים', () => {
    // 140 * 40/60 = 93.33 → 95
    expect(suggestLessonAmount(40, pricing)).toBe(95);
  });

  it('נופל לחישוב יחסי כשהמחירים לפי אורך אינם מוגדרים', () => {
    expect(suggestLessonAmount(45, { pricePerHour: 140, pricePer45: null, pricePer30: null })).toBe(105);
  });

  it('מכבד מחיר 0 של תלמיד/ה — יש שיעורים שלא גובים עליהם', () => {
    expect(suggestLessonAmount(60, pricing, { pricePerLesson: 0, durationMinutes: 60 })).toBe(0);
  });

  it('מחזיר 0 ולא NaN כשאין מחיר לשעה', () => {
    expect(suggestLessonAmount(50, { pricePerHour: 0 })).toBe(0);
  });
});

describe('suggestLessonAmountDetailed', () => {
  it('מסמן מחיר של תלמיד/ה כמקור student', () => {
    expect(suggestLessonAmountDetailed(45, pricing, { pricePerLesson: 100, durationMinutes: 45 }))
      .toEqual({ amount: 100, source: 'student', durationMinutes: 45 });
  });

  it('מסמן חישוב יחסי למחיר של התלמיד/ה כ-student-prorated', () => {
    expect(suggestLessonAmountDetailed(90, pricing, { pricePerLesson: 120, durationMinutes: 45 }))
      .toEqual({ amount: 240, source: 'student-prorated', durationMinutes: 90 });
  });

  it('מסמן התאמה בטבלת המחירון כ-exact', () => {
    expect(suggestLessonAmountDetailed(30, pricing)).toEqual({ amount: 90, source: 'exact', durationMinutes: 30 });
  });

  it('מסמן חישוב יחסי כ-prorated', () => {
    expect(suggestLessonAmountDetailed(90, pricing)).toEqual({ amount: 210, source: 'prorated', durationMinutes: 90 });
  });

  it('מסמן חוסר נתונים כ-unknown ולא ממציא סכום', () => {
    expect(suggestLessonAmountDetailed(45, { pricePerHour: 0 })).toEqual({ amount: 0, source: 'unknown', durationMinutes: 45 });
  });
});

describe('lessonDurationMinutes', () => {
  it('מודד את המשך בפועל מתוך זמני השיעור', () => {
    expect(lessonDurationMinutes('2026-09-10T14:00:00Z', '2026-09-10T14:45:00Z')).toBe(45);
  });
});
