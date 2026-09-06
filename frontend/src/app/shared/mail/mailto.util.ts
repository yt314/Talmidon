/**
 * בונה קישור ‎mailto:‎ עם נושא וגוף מוכנים, במקביל ל-‎buildWhatsappLink‎.
 * מחזיר null כשאין כתובת שנראית תקינה, כדי שהקורא יוכל להסתיר את הכפתור
 * במקום לפתוח חלון ריק.
 */
export function buildMailtoLink(
  email: string | null | undefined,
  subject?: string,
  body?: string
): string | null {
  if (!hasEmail(email)) return null;

  const params: string[] = [];
  // encodeURIComponent משאיר גרשיים ותווי עברית קריאים, ומקודד שורות חדשות כ-%0A
  if (subject) params.push(`subject=${encodeURIComponent(subject)}`);
  if (body) params.push(`body=${encodeURIComponent(body)}`);

  return `mailto:${email!.trim()}${params.length ? `?${params.join('&')}` : ''}`;
}

/**
 * בדיקה מכוונת-רופפת: תפקידה רק למנוע כפתור שפותח חלון ריק. ולידציה אמיתית
 * של כתובת נעשית בשרת, ופסילה אגרסיבית כאן הייתה חוסמת כתובות חוקיות.
 */
export function hasEmail(email: string | null | undefined): boolean {
  const value = email?.trim();
  return !!value && /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
}
