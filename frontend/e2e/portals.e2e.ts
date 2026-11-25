import type { Page } from '@playwright/test';
import { seedPractice, type Practice } from './seed';
import { dialog, expect, expectNoProblems, signIn, test, watchForProblems } from './support';

/**
 * פורטל ההורים ופורטל התלמידים, דרך הדפדפן ומול השרת והמסד האמיתיים: כל תפקיד רואה
 * בדיוק את מה שנועד לו. הרשאות ה-API עצמן נבדקות ב-backend; כאן נבדק שהמסכים מציגים
 * את מה שהשרת מחזיר, תחת ה-CSP של הפרודקשן.
 */
test.describe.configure({ mode: 'serial' });

let practice: Practice;

test.beforeAll(async () => {
  practice = await seedPractice();
});

/** בודק דרך ה-API של המורה שהפנייה שנשלחה מהפורטל הגיעה אליה. */
async function expectTeacherReceived(subject: string): Promise<void> {
  const api = await practice.teacherApi();
  const response = await api.get('messages');
  expect(response.ok()).toBe(true);
  expect(await response.text()).toContain(subject);
  await api.dispose();
}

async function sendMessage(page: Page, subject: string, child?: string): Promise<void> {
  await page.getByRole('button', { name: 'פנייה חדשה' }).first().click();
  const compose = dialog(page, 'פנייה חדשה למורה');
  if (child) {
    await compose.locator('#pm-child').click();
    await page.getByRole('option', { name: child }).click();
  }
  await compose.locator('#pm-subject').fill(subject);
  await compose.locator('#pm-body').fill('אפשר להזיז את השיעור של שבוע הבא?');
  await compose.getByRole('button', { name: 'שליחה' }).click();
  await expect(compose).toBeHidden();
  await expect(page.getByText(subject).first()).toBeVisible();
}

test.describe('הורה', () => {
  let page: Page;
  let problems: string[];

  test.beforeAll(async ({ browser }) => {
    page = await browser.newPage();
    problems = await watchForProblems(page);
  });

  test.afterEach(() => expectNoProblems(problems));

  test.afterAll(async () => {
    await page.close();
  });

  test('מתחבר ומגיע לפורטל, ורואה את הבת שלו', async () => {
    await signIn(page, practice.parent.email, practice.password);

    await expect(page).toHaveURL(/\/parent\/dashboard/);
    await expect(page.getByText(practice.student.name).first()).toBeVisible();
  });

  test('רואה את ההערות שגלויות להורים — וגם את זו שסומנה לתלמידה — אבל לא את הפרטית', async () => {
    await page.goto('/parent/notes');

    await expect(page.getByText(practice.notes.forParent)).toBeVisible();
    await expect(page.getByText(practice.notes.forStudent)).toBeVisible();
    await expect(page.getByText(practice.notes.private)).toHaveCount(0);
  });

  test('רואה את החיוב הפתוח על השיעור של אתמול', async () => {
    await page.goto('/parent/payments');

    const row = page.getByRole('row').filter({ hasText: practice.student.name });
    await expect(row).toContainText(`${practice.charge} ₪`);
  });

  test('רואה את חומר הלימוד שהמורה שיתפה', async () => {
    await page.goto('/parent/resources');

    await expect(page.getByText(practice.resourceTitle)).toBeVisible();
  });

  test('שולח פנייה למורה, והיא מגיעה אליה', async () => {
    const subject = `שאלה מההורה ${Date.now()}`;
    await page.goto('/parent/messages');
    await sendMessage(page, subject, practice.student.name);

    await expectTeacherReceived(subject);
  });

  test('מי שנכנס לאזור של המורה או לפורטל התלמידים — נשלח חזרה לפורטל שלו', async () => {
    await page.goto('/app/dashboard');
    await expect(page).toHaveURL(/\/parent\//);

    await page.goto('/student/notes');
    await expect(page).toHaveURL(/\/parent\//);
  });
});

test.describe('תלמידה', () => {
  let page: Page;
  let problems: string[];

  test.beforeAll(async ({ browser }) => {
    page = await browser.newPage();
    problems = await watchForProblems(page);
  });

  test.afterEach(() => expectNoProblems(problems));

  test.afterAll(async () => {
    await page.close();
  });

  test('מתחברת ומגיעה לפורטל שלה', async () => {
    await signIn(page, practice.student.email, practice.password);

    await expect(page).toHaveURL(/\/student\/dashboard/);
  });

  test('רואה רק את ההערה ששותפה איתה — לא את זו שלהורים ולא את הפרטית', async () => {
    await page.goto('/student/notes');

    await expect(page.getByText(practice.notes.forStudent)).toBeVisible();
    await expect(page.getByText(practice.notes.forParent)).toHaveCount(0);
    await expect(page.getByText(practice.notes.private)).toHaveCount(0);
  });

  test('רואה את חומר הלימוד שהמורה שיתפה', async () => {
    await page.goto('/student/resources');

    await expect(page.getByText(practice.resourceTitle)).toBeVisible();
  });

  test('לא רואה תשלומים: אין להם מקום בתפריט, והעמוד של ההורים שולח אותה חזרה', async () => {
    await page.goto('/student/dashboard');
    await expect(page.getByText('חומרי לימוד').first()).toBeVisible();
    await expect(page.getByText('תשלומים', { exact: true })).toHaveCount(0);

    await page.goto('/parent/payments');
    await expect(page).toHaveURL(/\/student\//);
    await expect(page.getByText(`${practice.charge} ₪`)).toHaveCount(0);
  });

  test('שולחת פנייה למורה, והיא מגיעה אליה', async () => {
    const subject = `שאלה מהתלמידה ${Date.now()}`;
    await page.goto('/student/messages');
    await sendMessage(page, subject);

    await expectTeacherReceived(subject);
  });
});
