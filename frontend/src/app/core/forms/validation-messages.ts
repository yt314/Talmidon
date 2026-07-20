import { AbstractControl } from '@angular/forms';

/** ממפה שגיאת ולידציה של שדה בטופס להודעה בעברית להצגה מתחת לשדה. */
export function fieldError(control: AbstractControl | null | undefined): string | null {
  if (!control || !control.errors || !(control.touched || control.dirty)) return null;
  const errors = control.errors;

  if (errors['required']) return 'שדה חובה';
  if (errors['email']) return 'כתובת אימייל לא תקינה';
  if (errors['minlength']) return `נדרשים לפחות ${errors['minlength'].requiredLength} תווים`;
  if (errors['maxlength']) return `מקסימום ${errors['maxlength'].requiredLength} תווים`;
  if (errors['min']) return `הערך המינימלי הוא ${errors['min'].min}`;
  if (errors['max']) return `הערך המקסימלי הוא ${errors['max'].max}`;
  if (errors['passwordMismatch']) return 'הסיסמאות אינן זהות';
  if (errors['dateRange']) return 'שעת הסיום חייבת להיות אחרי שעת ההתחלה';
  if (errors['passwordPolicy']) {
    const p = errors['passwordPolicy'];
    if (p.minlength) return 'הסיסמה חייבת להכיל לפחות 8 תווים';
    if (p.requiresUppercase) return 'הסיסמה חייבת להכיל לפחות אות גדולה אחת (A-Z)';
    if (p.requiresLowercase) return 'הסיסמה חייבת להכיל לפחות אות קטנה אחת (a-z)';
    if (p.requiresDigit) return 'הסיסמה חייבת להכיל לפחות ספרה אחת';
    return 'הסיסמה אינה עומדת בדרישות המדיניות';
  }

  return 'ערך לא תקין';
}

/** true אם יש להציג מסגרת "שגיאה" סביב שדה (גע/שינוי + שגיאה קיימת). */
export function isInvalid(control: AbstractControl | null | undefined): boolean {
  return !!control && control.invalid && (control.touched || control.dirty);
}
