import { describe, expect, it } from 'vitest';
import { IDLE_LIMIT_MS, IDLE_WARNING_MS, idleState, secondsUntilLogout } from './idle';

const minutes = (n: number) => n * 60_000;

/** ההחלטה מתי לנתק — לפי חותמות זמן, כך שגם מחשב שהתעורר משינה מנותק בזמן. */
describe('idle', () => {
  const last = 1_000_000;

  it('פעילה עד הדקה האחרונה', () => {
    expect(idleState(last, last)).toBe('active');
    expect(idleState(last, last + IDLE_LIMIT_MS - IDLE_WARNING_MS - 1)).toBe('active');
  });

  it('אזהרה בדקה האחרונה', () => {
    expect(idleState(last, last + IDLE_LIMIT_MS - IDLE_WARNING_MS)).toBe('warning');
    expect(idleState(last, last + IDLE_LIMIT_MS - 1)).toBe('warning');
  });

  it('מנותקת אחרי שלושים דקות — וגם אחרי שעות של שינה', () => {
    expect(IDLE_LIMIT_MS).toBe(minutes(30));
    expect(idleState(last, last + IDLE_LIMIT_MS)).toBe('expired');
    expect(idleState(last, last + minutes(600))).toBe('expired');
  });

  it('הספירה לאחור מעגלת כלפי מעלה, ואינה יורדת מתחת לאפס', () => {
    expect(secondsUntilLogout(last, last + IDLE_LIMIT_MS - 59_500)).toBe(60);
    expect(secondsUntilLogout(last, last + IDLE_LIMIT_MS - 1)).toBe(1);
    expect(secondsUntilLogout(last, last + IDLE_LIMIT_MS + 5_000)).toBe(0);
  });
});
