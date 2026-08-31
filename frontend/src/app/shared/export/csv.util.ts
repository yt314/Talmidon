/**
 * יוצר קובץ CSV מהנתונים ומוריד אותו בדפדפן. כולל BOM כדי ש-Excel יפתח עברית (UTF-8) כראוי.
 * הנתונים הם של המשתמש עצמו ונוצרים בצד הלקוח — אין שליחה לשרת חיצוני.
 */
export function downloadCsv(filename: string, rows: (string | number | null | undefined)[][]): void {
  const csv = rows.map(row => row.map(csvCell).join(',')).join('\r\n');
  const blob = new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

function csvCell(value: string | number | null | undefined): string {
  const text = value == null ? '' : String(value);
  return /[",\r\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
}
