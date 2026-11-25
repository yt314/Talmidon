import AxeBuilder from '@axe-core/playwright';
import type { Browser, Page } from '@playwright/test';
import { seedPractice, type Practice } from './seed';
import { expect, signIn, test } from './support';

/**
 * נגישות לפי WCAG 2.1 ברמה AA, בכל המסכים העיקריים של כל תפקיד: axe-core רץ על כל
 * עמוד, וכל הפרה מכשילה את הבדיקה עם רשימה של הכללים והאלמנטים. כך ניגודיות, תוויות
 * לשדות ושמות לכפתורים לא נשברים בשקט עם שינוי עיצוב.
 */
test.describe.configure({ mode: 'serial' });

let practice: Practice;

test.beforeAll(async () => {
  practice = await seedPractice();
});

/**
 * היומן (FullCalendar 7) גולל את השעות בתוך טבלה עם role="table". כשאין בשבוע שיעור שאפשר
 * להגיע אליו ב-Tab, axe מבקש לתת לאזור הגלילה tabindex — אבל אלמנט שמקבל פוקוס בתוך
 * הטבלה הוא הפרה חמורה יותר של מבנה ה-ARIA שלה. הכלל הזה מושבת לכן רק בעמודי היומן.
 */
const CALENDAR_PAGES = /\/(app|parent|student)\/lessons$/;

async function expectAccessible(page: Page, path: string): Promise<void> {
  await page.goto(path);
  await page.waitForLoadState('networkidle');
  // הנפשות הכניסה של PrimeNG משנות שקיפות, ובאמצען הניגודיות נמדדת על צבעים חלקיים
  await page.waitForTimeout(500);

  let axe = new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']);
  if (CALENDAR_PAGES.test(path)) axe = axe.disableRules(['scrollable-region-focusable']);
  const { violations } = await axe.analyze();

  const found = violations.flatMap(v => v.nodes.map(n => `${v.id} (${v.impact}): ${n.target.join(' ')} — ${v.help}`));
  expect(found, `accessibility violations on ${path}`).toEqual([]);
}

async function newPage(browser: Browser): Promise<Page> {
  // AxeBuilder צריך דף מתוך context שנפתח במפורש
  return (await browser.newContext()).newPage();
}

test('עמודים ציבוריים', async ({ browser }) => {
  const page = await newPage(browser);
  for (const path of ['/', '/login', '/register']) await expectAccessible(page, path);
  await page.close();
});

test('המורה', async ({ browser }) => {
  const page = await newPage(browser);
  await signIn(page, practice.teacher.email, practice.password);
  await expect(page).toHaveURL(/\/app\/setup/);
  await expectAccessible(page, '/app/setup');

  const subjects = page.locator('#s-subjects');
  await subjects.fill('מתמטיקה');
  await subjects.press('Enter');
  await page.locator('#s-price').fill('150');
  await page.locator('#s-city').fill('ירושלים');
  await page.locator('#s-phone').fill('050-0000000');
  await page.getByRole('button', { name: 'סיימתי' }).click();
  await expect(page).toHaveURL(/\/app\/dashboard/);

  for (const p of ['dashboard', 'students', 'lessons', 'payments', 'reports', 'messages', 'profile']) {
    await expectAccessible(page, `/app/${p}`);
  }
  await page.close();
});

test('הורה', async ({ browser }) => {
  const page = await newPage(browser);
  await signIn(page, practice.parent.email, practice.password);
  await expect(page).toHaveURL(/\/parent\/dashboard/);
  for (const p of ['dashboard', 'lessons', 'notes', 'payments', 'resources', 'messages']) {
    await expectAccessible(page, `/parent/${p}`);
  }
  await page.close();
});

test('תלמידה', async ({ browser }) => {
  const page = await newPage(browser);
  await signIn(page, practice.student.email, practice.password);
  await expect(page).toHaveURL(/\/student\/dashboard/);
  for (const p of ['dashboard', 'lessons', 'notes', 'resources', 'games', 'messages']) {
    await expectAccessible(page, `/student/${p}`);
  }
  await page.close();
});
