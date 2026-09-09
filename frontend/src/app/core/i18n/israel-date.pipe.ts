import { Pipe, PipeTransform } from '@angular/core';

/** הפורמטים שבשימוש בממשק. מכוון שיהיה סגור: כל פורמט חדש נוסף כאן במפורש. */
export type IsraelDateFormat = 'dd/MM/yyyy HH:mm' | 'dd/MM/yyyy' | 'dd/MM HH:mm' | 'HH:mm';

/**
 * מפרמט תאריך ושעה תמיד לפי שעון ישראל, ולא לפי אזור הזמן של הדפדפן.
 *
 * ‎DatePipe‎ של Angular מפרמט לפי אזור הזמן של המכשיר, ולכן אותו שיעור הוצג 18:00
 * בישראל, 16:00 בלונדון ו-11:00 בניו יורק. המוצר כולו פועל באזור זמן אחד (ראו
 * ‎AppTimeZone‎ בשרת), ושעת שיעור היא עובדה מקומית — הורה שהטלפון שלו מוגדר לאזור
 * זמן אחר קיבל שעה שגויה, ויכול היה לפספס שיעור.
 *
 * ‎Intl‎ ולא היסט קבוע: הפרמטר ‎timezone‎ של ‎DatePipe‎ מקבל היסט (‎+0300‎) ולא שם אזור,
 * והיסט קבוע נשבר במעבר שעון קיץ/חורף — בדיוק כמו שקרה בשרת (ראו ‎AppTimeZone.ToUtc‎).
 */
@Pipe({ name: 'ilDate' })
export class IsraelDatePipe implements PipeTransform {
  private static readonly formatter = new Intl.DateTimeFormat('en-GB', {
    timeZone: 'Asia/Jerusalem',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  });

  transform(
    value: Date | string | number | null | undefined,
    format: IsraelDateFormat = 'dd/MM/yyyy HH:mm'
  ): string | null {
    if (value === null || value === undefined || value === '') return null;

    const date = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(date.getTime())) return null;

    const p: Record<string, string> = {};
    for (const part of IsraelDatePipe.formatter.formatToParts(date)) p[part.type] = part.value;

    const day = `${p['day']}/${p['month']}`;
    const time = `${p['hour']}:${p['minute']}`;

    switch (format) {
      case 'dd/MM/yyyy HH:mm': return `${day}/${p['year']} ${time}`;
      case 'dd/MM/yyyy': return `${day}/${p['year']}`;
      case 'dd/MM HH:mm': return `${day} ${time}`;
      case 'HH:mm': return time;
    }
  }
}
