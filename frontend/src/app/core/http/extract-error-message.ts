/**
 * מחלץ הודעת שגיאה קריאה מתגובת שגיאת HTTP.
 *
 * גוף השגיאה מגיע בשני עולמות: הקונטרולרים שלנו מחזירים `errors` כמערך מחרוזות
 * או `message` יחיד, ואילו ולידציית המודל של ASP.NET מחזירה ProblemDetails —
 * שם `errors` הוא מילון של שדה אל רשימת הודעות. בלי הענף השני כל שגיאת
 * ולידציה מהשרת נבלעה והוחלפה בברירת המחדל.
 *
 * `title` של ProblemDetails מכוון במפורש: הוא בוילרפלייט באנגלית ("An error
 * occurred while processing your request"), ולכן גרוע יותר מהודעת ברירת המחדל
 * בעברית שהקורא סיפק לפי ההקשר.
 */
export function extractErrorMessage(err: unknown, fallback: string): string {
  const body = (err as { error?: unknown } | undefined)?.error;

  if (typeof body === 'string' && body.trim()) return body;
  if (!body || typeof body !== 'object') return fallback;

  const { errors, message, detail } = body as {
    errors?: unknown;
    message?: string;
    detail?: string;
  };

  if (Array.isArray(errors) && errors.length) return errors.join(' ');

  // ProblemDetails: אוספים את ההודעות מכל השדות לפי סדר הופעתם
  if (errors && typeof errors === 'object') {
    const flat = Object.values(errors as Record<string, unknown>)
      .flatMap(v => (Array.isArray(v) ? v : [v]))
      .filter((v): v is string => typeof v === 'string' && v.trim().length > 0);
    if (flat.length) return flat.join(' ');
  }

  return message ?? detail ?? fallback;
}