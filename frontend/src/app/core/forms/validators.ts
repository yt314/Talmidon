import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * תואם למדיניות הסיסמה בפועל בשרת (ASP.NET Identity): 8+ תווים, לפחות ספרה אחת,
 * אות גדולה אחת ואות קטנה אחת. RequireNonAlphanumeric=false, לכן לא נדרש תו מיוחד.
 */
export function passwordPolicyValidator(control: AbstractControl): ValidationErrors | null {
  const value: string = control.value ?? '';
  if (!value) return null;

  const errors: Record<string, boolean> = {};
  if (value.length < 8) errors['minlength'] = true;
  if (!/[0-9]/.test(value)) errors['requiresDigit'] = true;
  if (!/[a-z]/.test(value)) errors['requiresLowercase'] = true;
  if (!/[A-Z]/.test(value)) errors['requiresUppercase'] = true;

  return Object.keys(errors).length > 0 ? { passwordPolicy: errors } : null;
}

/** ולידציית קבוצה: ששני שדות הסיסמה זהים. שם השגיאה מוצב על שדה האימות. */
export function passwordsMatchValidator(passwordKey: string, confirmKey: string): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const password = group.get(passwordKey)?.value;
    const confirm = group.get(confirmKey)?.value;
    const confirmControl = group.get(confirmKey);
    if (!confirmControl || !confirm) return null;

    if (password !== confirm) {
      confirmControl.setErrors({ ...confirmControl.errors, passwordMismatch: true });
    } else if (confirmControl.hasError('passwordMismatch')) {
      const { passwordMismatch, ...rest } = confirmControl.errors ?? {};
      confirmControl.setErrors(Object.keys(rest).length > 0 ? rest : null);
    }
    return null;
  };
}

/** ולידציית קבוצה: שעת הסיום חייבת להיות אחרי שעת ההתחלה. תואם לבדיקה המקבילה ב-backend. */
export function endAfterStartValidator(startKey: string, endKey: string): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const start: Date | null = group.get(startKey)?.value;
    const end: Date | null = group.get(endKey)?.value;
    if (!start || !end) return null;
    return end > start ? null : { dateRange: true };
  };
}
