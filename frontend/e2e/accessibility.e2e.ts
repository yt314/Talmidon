import AxeBuilder from '@axe-core/playwright';
import type { Browser, Page } from '@playwright/test';
import { SCREENS } from './screens';
import { seedPractice, type Practice } from './seed';
import { completeSetup, expect, signIn, test } from './support';

/**
 * נגישות לפי WCAG 2.1 ברמה AA, בכל המסכים העיקריים של כל תפקיד, במצב בהיר ובמצב כהה:
 * axe-core רץ על כל עמוד, וכל הפרה מכשילה את הבדיקה עם רשימה של הכללים והאלמנטים. כך
 * ניגודיות, תוויות לשדות ושמות לכפתורים לא נשברים בשקט עם שינוי עיצוב — גם בצבעים של
 * המצב הכהה, שאיש לא רואה כשעובדים במצב בהיר.
 */
test.describe.configure({ mode: 'serial' });

/**
 * היומן (FullCalendar 7) גולל את השעות בתוך טבלה עם role="table". כשאין בשבוע שיעור שאפשר
 * להגיע אליו ב-Tab, axe מבקש לתת לאזור הגלילה tabindex — אבל אלמנט שמקבל פוקוס בתוך
 * הטבלה הוא הפרה חמורה יותר של מבנה ה-ARIA שלה. הכלל הזה מושבת לכן רק בעמודי היומן.
 */
const CALENDAR_PAGES = /\/(app|parent|student)\/lessons$/;

async function expectAccessible(page: Page, path: string, colorScheme: 'light' | 'dark'): Promise<void> {
  await page.goto(path);
  await page.waitForLoadState('networkidle');
  // האתר עוקב אחרי ההעדפה של מערכת ההפעלה, כל עוד לא נבחר מצב במתג
  const html = page.locator('html');
  await (colorScheme === 'dark' ? expect(html).toHaveClass(/app-dark/) : expect(html).not.toHaveClass(/app-dark/));
  // הנפשות הכניסה של PrimeNG משנות שקיפות, ובאמצען הניגודיות נמדדת על צבעים חלקיים
  await page.waitForTimeout(500);

  let axe = new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']);
  if (CALENDAR_PAGES.test(path)) axe = axe.disableRules(['scrollable-region-focusable']);
  const { violations } = await axe.analyze();

  const found = violations.flatMap(v => v.nodes.map(n => `${v.id} (${v.impact}): ${n.target.join(' ')} — ${v.help}`));
  expect(found, `accessibility violations on ${path}`).toEqual([]);
}

for (const colorScheme of ['light', 'dark'] as const) {
  test.describe(colorScheme === 'light' ? 'מצב בהיר' : 'מצב כהה', () => {
    let practice: Practice;

    test.beforeAll(async () => {
      practice = await seedPractice();
    });

    async function newPage(browser: Browser): Promise<Page> {
      // AxeBuilder צריך דף מתוך context שנפתח במפורש
      return (await browser.newContext({ colorScheme })).newPage();
    }

    const check = (page: Page, path: string) => expectAccessible(page, path, colorScheme);

    test('עמודים ציבוריים', async ({ browser }) => {
      const page = await newPage(browser);
      for (const path of SCREENS.public) await check(page, path);
      await page.close();
    });

    test('המורה', async ({ browser }) => {
      const page = await newPage(browser);
      await signIn(page, practice.teacher.email, practice.password);
      await expect(page).toHaveURL(/\/app\/setup/);
      await check(page, '/app/setup');
      await completeSetup(page);
      for (const path of SCREENS.teacher) await check(page, path);
      await page.close();
    });

    test('הורה', async ({ browser }) => {
      const page = await newPage(browser);
      await signIn(page, practice.parent.email, practice.password);
      await expect(page).toHaveURL(/\/parent\/dashboard/);
      for (const path of SCREENS.parent) await check(page, path);
      await page.close();
    });

    test('תלמידה', async ({ browser }) => {
      const page = await newPage(browser);
      await signIn(page, practice.student.email, practice.password);
      await expect(page).toHaveURL(/\/student\/dashboard/);
      for (const path of SCREENS.student) await check(page, path);
      await page.close();
    });
  });
}
