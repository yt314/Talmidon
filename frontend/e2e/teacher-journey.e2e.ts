import type { Page } from '@playwright/test';
import { confirmEmail, dialog, expect, expectNoProblems, test, toast, uniqueEmail, watchForProblems } from './support';

/**
 * הדרך של מורה חדשה מההרשמה ועד תשלום ראשון — דרך הדפדפן, מול השרת והמסד האמיתיים,
 * ותחת ה-CSP של הפרודקשן. כל צעד נשען על הקודם, ולכן הם רצים בסדר ובאותו דפדפן.
 */
test.describe.configure({ mode: 'serial' });

const email = uniqueEmail('teacher');
const password = 'Secret123';

/** השמות ייחודיים להרצה, כדי שהרצה חוזרת על אותו מסד לא תמצא את המורה מהקודמת. */
const run = String(Date.now()).slice(-6);
const teacherName = `רחל ${run}`;
const parentName = `אמא ${run}`;
const studentName = `דנה ${run}`;
const visitorName = `פונה ${run}`;

/** dd/mm/yyyy של אתמול — שיעור שכבר עבר, וממתין לסימון. */
const yesterday = () => {
  const d = new Date();
  d.setDate(d.getDate() - 1);
  return [d.getDate(), d.getMonth() + 1, d.getFullYear()].map(n => String(n).padStart(2, '0')).join('/');
};

test.describe('מורה חדשה', () => {
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

  test('נרשמת', async () => {
    await page.goto('/register');
    await page.locator('#fullName').fill(teacherName);
    await page.locator('#email').fill(email);
    await page.locator('#password').fill(password);
    await page.locator('#confirmPassword').fill(password);
    await page.locator('button[type=submit]').click();

    await expect(page.getByRole('heading', { name: 'כמעט סיימנו!' })).toBeVisible();
  });

  test('אחרי אישור המייל מתחברת, ונשלחת להשלים את הפרופיל', async () => {
    confirmEmail(email);

    await page.goto('/login');
    await page.locator('#email').fill(email);
    await page.locator('#password').fill(password);
    await page.locator('button[type=submit]').click();

    await expect(page).toHaveURL(/\/app\/setup/);
  });

  test('משלימה את הפרופיל ומגיעה ללוח הבקרה', async () => {
    const subjects = page.locator('#s-subjects');
    await subjects.fill('מתמטיקה');
    await subjects.press('Enter');
    await page.locator('#s-price').fill('150');
    await page.locator('#s-city').fill('ירושלים');
    await page.locator('#s-phone').fill('050-0000000');
    await page.getByRole('button', { name: 'סיימתי' }).click();

    await expect(page).toHaveURL(/\/app\/dashboard/);
    await expect(page.getByText('להוסיף תלמיד ראשון')).toBeVisible();
  });

  test('מוסיפה הורה, ותלמידה שמקושרת אליו', async () => {
    await page.goto('/app/students');
    await page.getByRole('tab', { name: 'הורים' }).click();
    await page.getByRole('button', { name: 'הוסף הורה' }).click();
    const parent = dialog(page, 'הוספת הורה');
    await parent.locator('#p-fullName').fill(parentName);
    await parent.locator('#p-gender').click();
    await page.getByRole('option', { name: 'נקבה' }).click();
    await parent.locator('#p-email').fill(uniqueEmail('parent'));
    await parent.getByRole('button', { name: 'שמירה' }).click();
    await expect(parent).toBeHidden();

    await page.goto('/app/students');
    await page.getByRole('button', { name: 'הוסף תלמיד' }).click();
    const student = dialog(page, 'הוספת תלמיד');
    await student.locator('#s-fullName').fill(studentName);
    await student.locator('#s-gender').click();
    await page.getByRole('option', { name: 'נקבה' }).click();
    await student.locator('p-multiselect').click();
    await page.getByRole('option', { name: parentName }).click();
    await student.locator('.p-dialog-title').click();
    await student.getByRole('button', { name: 'שמירה' }).click();
    await expect(student).toBeHidden();

    await page.goto('/app/students');
    await expect(page.getByText(studentName)).toBeVisible();
  });

  test('קובעת שיעור — ביום שכבר עבר', async () => {
    await page.goto('/app/lessons');
    await page.getByRole('button', { name: 'הוסף שיעור' }).click();
    const add = dialog(page, 'הוספת שיעור');
    await add.locator('#l-student').click();
    await page.getByRole('option', { name: studentName }).click();
    await add.locator('#l-date').pressSequentially(yesterday());
    await add.locator('.p-dialog-title').click();
    await add.locator('#l-start').pressSequentially('10:00');
    await add.locator('#l-start').press('Tab');
    await add.locator('.p-dialog-title').click();
    await add.getByRole('button', { name: 'שמירה' }).click();

    await expect(toast(page, 'השיעור נוסף בהצלחה')).toBeVisible();
  });

  test('מסמנת שהשיעור התקיים, והחיוב נרשם', async () => {
    await page.goto('/app/lessons');
    await page.getByRole('button', { name: 'סמן את הבא' }).click();
    const complete = dialog(page, 'סיום שיעור');
    await expect(complete.locator('#c-amount')).toHaveValue(/150/);
    await complete.getByRole('button', { name: 'שמירה' }).click();

    await expect(toast(page, 'השיעור עודכן')).toBeVisible();
  });

  test('גובה את התשלום: החיוב נסגר, והתשלום מופיע בהיסטוריה', async () => {
    await page.goto('/app/payments');
    const row = page.locator('tr', { hasText: parentName });
    await expect(row).toContainText('150');
    await row.getByRole('button', { name: 'גבייה' }).click();
    await page.getByRole('button', { name: 'סמן כשולם ושלח אישור להורה' }).click();

    await expect(page.getByText('אין חיובים פתוחים להורה זה')).toBeVisible();
    await page.getByRole('tab', { name: 'היסטוריית תשלומים' }).click();
    await expect(page.locator('tr', { hasText: parentName })).toContainText('150');
  });

  /** הורה שאינה מחוברת: מוצאת את המורה בספרייה ושולחת פנייה מהדף שלה. */
  test('הורה מוצאת אותה בספרייה הציבורית ושולחת פנייה', async ({ browser }) => {
    const visitorContext = await browser.newContext();
    const visitor = await visitorContext.newPage();
    const visitorProblems = await watchForProblems(visitor);

    await visitor.goto('/');
    await visitor.getByRole('textbox', { name: 'חיפוש מורה לפי שם, עיר או שכונה' }).fill(teacherName);
    await expect(visitor.getByText('נמצאה מורה אחת')).toBeVisible();
    await visitor.getByRole('link', { name: 'לפרופיל המלא' }).click();

    await expect(visitor.getByRole('heading', { name: teacherName })).toBeVisible();
    await visitor.locator('#c-name').fill(visitorName);
    await visitor.locator('#c-phone').fill('052-0000000');
    await visitor.locator('#c-message').fill('מחפשת מורה למתמטיקה לכיתה ה׳');
    await visitor.getByRole('button', { name: 'שליחת פנייה' }).click();
    await expect(visitor.getByText('הפנייה נשלחה')).toBeVisible();

    expectNoProblems(visitorProblems);
    await visitorContext.close();
  });

  test('הפנייה מגיעה אל המורה', async () => {
    await page.goto('/app/contact-requests');
    await expect(page.getByText(visitorName)).toBeVisible();
  });
});
