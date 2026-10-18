import { describe, expect, it } from 'vitest';
import { extractErrorMessage } from './extract-error-message';

const FALLBACK = 'העדכון נכשל.';

describe('extractErrorMessage', () => {
  /**
   * הבקשה לא הגיעה לשרת. "העדכון נכשל" משאיר פתוחה את השאלה אם השינוי נשמר,
   * ודווקא כאן התשובה ידועה: לא, ושווה לנסות שוב.
   */
  it('אומר שאין חיבור כשהבקשה לא הגיעה לשרת', () => {
    // מה ש-Angular מוסר בכשל רשת: status 0, וב-error אירוע דפדפן ולא גוף תשובה
    expect(extractErrorMessage({ status: 0, error: { type: 'error' } }, FALLBACK)).toContain('חיבור');
  });

  it('לא מתבלבל בין "אין חיבור" לבין שגיאה אמיתית מהשרת', () => {
    expect(extractErrorMessage({ status: 409, error: { message: 'השיעור כבר הושלם.' } }, FALLBACK))
      .toBe('השיעור כבר הושלם.');
  });

  it('מעדיף את ההודעה של הקונטרולר', () => {
    expect(extractErrorMessage({ status: 400, error: { message: 'תלמיד לא נמצא.' } }, FALLBACK))
      .toBe('תלמיד לא נמצא.');
  });

  it('מאחד רשימת שגיאות למשפט אחד', () => {
    expect(extractErrorMessage({ status: 400, error: { errors: ['שדה חסר.', 'ערך לא תקין.'] } }, FALLBACK))
      .toBe('שדה חסר. ערך לא תקין.');
  });

  /** ProblemDetails של ASP.NET: errors הוא מילון של שדה אל רשימת הודעות. */
  it('שולף גם ולידציה של ASP.NET, שמגיעה כמילון', () => {
    const body = { errors: { Email: ['כתובת לא תקינה.'], Password: ['קצרה מדי.'] } };
    expect(extractErrorMessage({ status: 400, error: body }, FALLBACK)).toBe('כתובת לא תקינה. קצרה מדי.');
  });

  /** ה-title של ProblemDetails הוא בוילרפלייט באנגלית — גרוע מברירת המחדל בעברית. */
  it('נופל לברירת המחדל כשאין בגוף שום דבר קריא', () => {
    expect(extractErrorMessage({ status: 500, error: { title: 'An error occurred' } }, FALLBACK)).toBe(FALLBACK);
    expect(extractErrorMessage(undefined, FALLBACK)).toBe(FALLBACK);
  });

  /**
   * קוד התקלה הוא מה שהופך "הפעולה נכשלה" למשהו שאפשר למסור, ובצד השני למצוא לפיו
   * שורה עם עקבת מחסנית. לכן הוא מצורף כאן, פעם אחת, ולא בכל מסך בנפרד.
   */
  it('מצרף את קוד התקלה להודעת השרת', () => {
    const body = { message: 'אירעה תקלה בשרת.', reference: 'ABC-234' };
    expect(extractErrorMessage({ status: 500, error: body }, FALLBACK))
      .toBe('אירעה תקלה בשרת. (קוד תקלה: ABC-234)');
  });

  it('מצרף את הקוד גם כשההודעה עצמה היא ברירת המחדל', () => {
    expect(extractErrorMessage({ status: 500, error: { reference: 'ABC-234' } }, FALLBACK))
      .toBe(`${FALLBACK} (קוד תקלה: ABC-234)`);
  });

  it('לא מוסיף כלום כשאין קוד', () => {
    expect(extractErrorMessage({ status: 409, error: { message: 'השיעור כבר הושלם.' } }, FALLBACK))
      .toBe('השיעור כבר הושלם.');
  });
});
