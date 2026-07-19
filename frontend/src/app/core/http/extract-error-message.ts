/** מחלץ הודעת שגיאה קריאה מתגובת שגיאת HTTP (errors[] או message), עם ברירת מחדל. */
export function extractErrorMessage(err: unknown, fallback: string): string {
  const body = (err as { error?: { errors?: string[]; message?: string } } | undefined)?.error;
  if (body?.errors?.length) return body.errors.join(' ');
  return body?.message ?? fallback;
}
