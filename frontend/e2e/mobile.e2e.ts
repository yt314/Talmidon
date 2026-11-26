import type { Locator, Page } from '@playwright/test';
import { SCREENS } from './screens';
import { seedPractice, type Practice } from './seed';
import { completeSetup, dialog, expect, signIn, test } from './support';

/**
 * האתר בטלפון צר (360 פיקסלים, הרוחב של רוב מכשירי האנדרואיד הזולים): בכל מסך עיקרי של
 * כל תפקיד העמוד לא גולל לרוחב והסרגל העליון נשאר שורה אחת, ודיאלוגים נכנסים במסך.
 * אלה הכללים של DESIGN.md לטלפון, ואף בדיקה אחרת לא רצה ברוחב הזה.
 */
test.describe.configure({ mode: 'serial' });
test.use({ viewport: { width: 360, height: 740 }, isMobile: true, hasTouch: true });

let practice: Practice;

test.beforeAll(async () => {
  practice = await seedPractice();
});

/** הסרגל של העמודים הציבוריים, או של המעטפת של כל תפקיד אחרי ההתחברות. */
const topBar = (page: Page): Locator => page.locator('.library-header, .app-shell-topbar .p-menubar').first();

/** מסכי ההתחברות וההרשמה הם כרטיס בלי סרגל עליון, ולכן נבדקת בהם רק הגלילה לרוחב. */
const WITHOUT_TOP_BAR = new Set(['/login', '/register']);

async function expectFitsPhone(page: Page, path: string): Promise<void> {
  await page.goto(path);
  await page.waitForLoadState('networkidle');

  // מה שבולט מעבר לקצה המסך. בעברית התוכן נשפך שמאלה, ולכן נבדקים שני הצדדים.
  const overflow = await page.evaluate(() => {
    const width = document.documentElement.clientWidth;
    if (document.documentElement.scrollWidth <= width) return [];
    return [...document.body.querySelectorAll<HTMLElement>('*')]
      .filter(el => {
        const r = el.getBoundingClientRect();
        return r.width > 0 && (r.right > width + 1 || r.left < -1);
      })
      .slice(0, 5)
      .map(el => `${el.tagName.toLowerCase()}.${[...el.classList].join('.')} (${Math.round(el.getBoundingClientRect().width)}px)`);
  });
  expect(overflow, `${path} scrolls sideways on a phone`).toEqual([]);

  if (WITHOUT_TOP_BAR.has(path)) return;
  // שורה אחת: כל הפריטים הגלויים בסרגל חולקים פס גובה משותף
  const rows = await topBar(page).evaluate(bar => {
    const items = [...bar.children]
      .map(el => el.getBoundingClientRect())
      .filter(r => r.width > 0 && r.height > 0);
    return Math.max(...items.map(r => r.top)) < Math.min(...items.map(r => r.bottom)) ? 1 : 2;
  });
  expect(rows, `the top bar on ${path} wraps onto a second row`).toBe(1);
}

async function expectDialogFits(page: Page, opener: string, title: string): Promise<void> {
  await page.getByRole('button', { name: opener }).click();
  const box = await dialog(page, title).boundingBox();
  expect(box, `the "${title}" dialog did not open`).not.toBeNull();
  const width = page.viewportSize()!.width;
  expect(box!.x, `the "${title}" dialog starts off screen`).toBeGreaterThanOrEqual(0);
  expect(box!.x + box!.width, `the "${title}" dialog ends off screen`).toBeLessThanOrEqual(width);
  await page.keyboard.press('Escape');
  await expect(dialog(page, title)).toBeHidden();
}

test('עמודים ציבוריים', async ({ page }) => {
  for (const path of SCREENS.public) await expectFitsPhone(page, path);
});

test('המורה', async ({ page }) => {
  await signIn(page, practice.teacher.email, practice.password);
  await expect(page).toHaveURL(/\/app\/setup/);
  await expectFitsPhone(page, '/app/setup');
  await completeSetup(page);
  for (const path of SCREENS.teacher) await expectFitsPhone(page, path);

  await page.goto('/app/students');
  await expectDialogFits(page, 'הוסף תלמיד', 'הוספת תלמיד');
  await page.goto('/app/lessons');
  await expectDialogFits(page, 'הוסף שיעור', 'הוספת שיעור');
});

test('הורה', async ({ page }) => {
  await signIn(page, practice.parent.email, practice.password);
  await expect(page).toHaveURL(/\/parent\/dashboard/);
  for (const path of SCREENS.parent) await expectFitsPhone(page, path);
});

test('תלמידה', async ({ page }) => {
  await signIn(page, practice.student.email, practice.password);
  await expect(page).toHaveURL(/\/student\/dashboard/);
  for (const path of SCREENS.student) await expectFitsPhone(page, path);
});
