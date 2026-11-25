import { execFileSync } from 'node:child_process';
import { test as base, expect, type Locator, type Page } from '@playwright/test';
import { psqlEnvironment } from './database';

/** שאילתה ישירה למסד — רק למה שאין לו דרך במוצר בלי מייל אמיתי. */
export function sql(query: string): string {
  return execFileSync('psql', ['-qAtc', query], { encoding: 'utf-8', env: { ...process.env, ...psqlEnvironment } }).trim();
}

/**
 * מאשר כתובת מייל כאילו נלחץ הקישור שנשלח אליה. השליחה עצמה נכשלת בבדיקות (אין שרת
 * דואר) והשרת ממשיך בלעדיה; מסלול הקישור עצמו מכוסה בבדיקות ה-backend.
 */
export function confirmEmail(email: string): void {
  const updated = sql(`update "AspNetUsers" set "EmailConfirmed" = true where "NormalizedEmail" = upper('${email}') returning 1`);
  expect(updated, `no account for ${email}`).toBe('1');
}

/** כתובת שלא הייתה קיימת בהרצה קודמת על אותו מסד. */
export const uniqueEmail = (name: string) => `${name}-${Date.now()}@e2e.example`;

export const dialog = (page: Page, title: string): Locator =>
  page.locator('.p-dialog').filter({ has: page.locator('.p-dialog-title', { hasText: title }) });

export const toast = (page: Page, text: string): Locator => page.locator('.p-toast-message', { hasText: text }).first();

/** התחברות דרך מסך ההתחברות. */
export async function signIn(page: Page, email: string, password: string): Promise<void> {
  await page.goto('/login');
  await page.locator('#email').fill(email);
  await page.locator('#password').fill(password);
  await page.locator('button[type=submit]').click();
}

/**
 * סוגר בוחר תאריך או שעה פתוח בלחיצה על כותרת הדיאלוג. בוחר שעדיין באמצע אנימציית
 * הפתיחה (למשל של השדה הבא, שקיבל פוקוס ב-Tab) יכול להתעלם מלחיצה אחת ולהישאר פתוח
 * מעל הכפתורים שמתחתיו — ולכן הלחיצה חוזרת עד שלא נשאר אף בוחר פתוח.
 */
export async function closePickers(page: Page, dialogLocator: Locator): Promise<void> {
  await expect(async () => {
    await dialogLocator.locator('.p-dialog-title').click();
    await expect(page.locator('.p-datepicker-panel:not(.p-datepicker-panel-inline)')).toHaveCount(0, { timeout: 1_000 });
  }).toPass({ timeout: 10_000 });
}

/**
 * אוסף את מה שהדפדפן חסם לפי ה-CSP ואת השגיאות שנזרקו בדף. אלה בדיוק התקלות שלא
 * מופיעות בשום בנייה ובשום בדיקת יחידה — האתר פשוט מפסיק לעבוד בשקט.
 */
export async function watchForProblems(page: Page): Promise<string[]> {
  const problems: string[] = [];
  await page.addInitScript(() =>
    document.addEventListener('securitypolicyviolation', e =>
      console.error(`CSP violation: ${e.violatedDirective} blocked ${e.blockedURI || 'inline'}`)
    )
  );
  page.on('console', message => {
    if (message.type() === 'error' && /CSP violation|Content Security Policy/.test(message.text())) {
      problems.push(message.text());
    }
  });
  page.on('pageerror', error => problems.push(`page error: ${error.message}`));
  return problems;
}

export const expectNoProblems = (problems: string[]) =>
  expect(problems, 'the browser blocked something or a script failed').toEqual([]);

/** כל בדיקה שמשתמשת בדף הרגיל נכשלת אם נחסם בו משהו. */
export const test = base.extend<{ pageProblems: string[] }>({
  pageProblems: [
    async ({ page }, use) => {
      const problems = await watchForProblems(page);
      await use(problems);
      expectNoProblems(problems);
    },
    { auto: true }
  ]
});

export { expect };
