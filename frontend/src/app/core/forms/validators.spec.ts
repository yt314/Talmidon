import { FormControl, FormGroup } from '@angular/forms';
import { describe, expect, it } from 'vitest';
import { endAfterStartValidator, httpUrlValidator, passwordPolicyValidator, passwordsMatchValidator } from './validators';

/**
 * הכללים כאן מקבילים לאלה שבשרת. כשהם נפרדים, הטופס מאשר סיסמה שהשרת דוחה — והמשתמשת
 * מקבלת שגיאה רק אחרי השליחה, במקום ליד השדה.
 */
describe('passwordPolicyValidator', () => {
  const check = (value: string) => passwordPolicyValidator(new FormControl(value));

  it('סיסמה שעומדת במדיניות של השרת עוברת', () => {
    expect(check('Abcdefg1')).toBeNull();
  });

  it('שדה ריק נשאר בידי required', () => {
    expect(check('')).toBeNull();
  });

  it.each([
    ['Abcdef1', 'minlength'],
    ['Abcdefgh', 'requiresDigit'],
    ['ABCDEFG1', 'requiresLowercase'],
    ['abcdefg1', 'requiresUppercase']
  ])('%s נדחית: %s', (value, rule) => {
    expect(check(value)).toEqual({ passwordPolicy: { [rule]: true } });
  });

  it('תו מיוחד אינו נדרש, כמו בשרת', () => {
    expect(check('Abcdefgh12')).toBeNull();
  });
});

describe('passwordsMatchValidator', () => {
  const form = () =>
    new FormGroup(
      { password: new FormControl('Abcdefg1'), confirm: new FormControl('') },
      { validators: passwordsMatchValidator('password', 'confirm') }
    );

  it('שגיאת אי-ההתאמה מוצבת על שדה האימות', () => {
    const group = form();
    group.controls.confirm.setValue('Other1234');
    expect(group.controls.confirm.hasError('passwordMismatch')).toBe(true);
  });

  it('תיקון האימות מסיר את השגיאה', () => {
    const group = form();
    group.controls.confirm.setValue('Other1234');
    group.controls.confirm.setValue('Abcdefg1');
    expect(group.controls.confirm.errors).toBeNull();
    expect(group.valid).toBe(true);
  });
});

describe('endAfterStartValidator', () => {
  const at = (hour: number) => new Date(2026, 2, 1, hour);
  const group = (start: Date | null, end: Date | null) =>
    new FormGroup({ start: new FormControl(start), end: new FormControl(end) }, { validators: endAfterStartValidator('start', 'end') });

  it('סיום אחרי התחלה — תקין', () => expect(group(at(9), at(10)).valid).toBe(true));
  it('סיום בדיוק בהתחלה — שגוי', () => expect(group(at(9), at(9)).hasError('dateRange')).toBe(true));
  it('שדה חסר נשאר בידי required', () => expect(group(at(9), null).valid).toBe(true));
});

describe('httpUrlValidator', () => {
  const check = (value: string) => httpUrlValidator(new FormControl(value));

  it.each(['https://example.com/a', 'http://example.com'])('%s תקינה', url => expect(check(url)).toBeNull());

  /** הקישור מוצג כעוגן לחיץ בפורטל של מישהו אחר — javascript: היה רץ שם. */
  it.each(['javascript:alert(1)', 'ftp://example.com', 'example.com'])('%s נדחית', url =>
    expect(check(url)).toEqual({ httpUrl: true })
  );
});
