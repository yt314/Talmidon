/**
 * תאריך בלי שעה בפורמט ‎yyyy-MM-dd‎ — מה שהשרת מצפה לו בשדות DateOnly (תאריך לידה,
 * תאריך תשלום).
 *
 * לפי השעון המקומי ולא ‎toISOString()‎: בחירה ב-datepicker היא חצות מקומית, ובישראל
 * (UTC+2/+3) ‎toISOString()‎ הייתה מחזירה את היום שלפניו.
 */
export function toDateOnly(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}
