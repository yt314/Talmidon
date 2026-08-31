/**
 * בונה קישור "וואטסאפ" (wa.me) למספר טלפון ישראלי, עם טקסט מוכן אופציונלי.
 * ממיר מספר מקומי (05X-XXXXXXX) לפורמט בינלאומי (9725XXXXXXXX).
 * מחזיר null אם אין מספר תקין.
 */
export function buildWhatsappLink(phone: string | null | undefined, text?: string): string | null {
  const international = toInternational(phone);
  if (!international) return null;
  const query = text ? `?text=${encodeURIComponent(text)}` : '';
  return `https://wa.me/${international}${query}`;
}

/** האם ניתן לבנות קישור וואטסאפ מהמספר הנתון. */
export function hasWhatsapp(phone: string | null | undefined): boolean {
  return toInternational(phone) !== null;
}

function toInternational(phone: string | null | undefined): string | null {
  if (!phone) return null;
  let digits = phone.replace(/\D/g, '');
  if (!digits) return null;

  // 00972... → 972...
  if (digits.startsWith('00')) digits = digits.slice(2);
  // 0XX... (מקומי) → 972XX...
  if (digits.startsWith('0')) digits = `972${digits.slice(1)}`;
  // מספר ללא קידומת מדינה שנראה כמו נייד ישראלי (5XXXXXXXX)
  else if (!digits.startsWith('972') && digits.length === 9 && digits.startsWith('5')) digits = `972${digits}`;

  // מספר בינלאומי סביר
  return digits.length >= 11 && digits.length <= 15 ? digits : null;
}
