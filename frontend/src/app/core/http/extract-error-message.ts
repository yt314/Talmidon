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
 *
 * `reference` הוא קוד התקלה שהשרת מחזיר על כל שגיאה שלא טופלה. הוא מצורף כאן,
 * בנקודה אחת, ולא בכל מסך בנפרד: בלעדיו "הפעולה נכשלה" הוא כל מה שנשאר, ואיתו
 * יש משהו למסור — ובצד השני שורה עם עקבת מחסנית.
 */
export function extractErrorMessage(err: unknown, fallback: string): string {
  // ‎status 0‎ — הבקשה לא הגיעה לשרת בכלל: אין חיבור, או שהשרת אינו עונה. ההבדל
  // חשוב: "העדכון נכשל" משאיר פתוחה את השאלה אם השינוי נשמר, ו"אין חיבור" גם
  // עונה עליה וגם אומר מה לעשות.
  if ((err as { status?: number } | undefined)?.status === 0) {
    return 'אין חיבור לשרת. בדקי את החיבור לאינטרנט ונסי שוב.';
  }

  const body = (err as { error?: unknown } | undefined)?.error;

  if (typeof body === 'string' && body.trim()) return body;
  if (!body || typeof body !== 'object') return fallback;

  const { errors, message, detail } = body as {
    errors?: unknown;
    message?: string;
    detail?: string;
  };

  if (Array.isArray(errors) && errors.length) return withReference(errors.join(' '), body);

  // ProblemDetails: אוספים את ההודעות מכל השדות לפי סדר הופעתם
  if (errors && typeof errors === 'object') {
    const flat = Object.values(errors as Record<string, unknown>)
      .flatMap(v => (Array.isArray(v) ? v : [v]))
      .filter((v): v is string => typeof v === 'string' && v.trim().length > 0);
    if (flat.length) return withReference(flat.join(' '), body);
  }

  return withReference(message ?? detail ?? fallback, body);
}

/** מוסיף את קוד התקלה להודעה, אם השרת שלח אחד. */
function withReference(message: string, body: unknown): string {
  const reference = (body as { reference?: unknown } | undefined)?.reference;
  return typeof reference === 'string' && reference.trim()
    ? `${message} (קוד תקלה: ${reference.trim()})`
    : message;
}
