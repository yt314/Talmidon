import { request, type APIRequestContext } from '@playwright/test';
import { confirmEmail, expect, sql, uniqueEmail } from './support';
import { environment } from '../src/environments/environment.e2e';

/**
 * מורה עם הורה ותלמידה, שיעורים, הערות, חיוב וחומר לימוד — נבנים דרך ה-API ולא דרך
 * המסכים של המורה, שכבר נבדקים ב-teacher-journey.e2e.ts. הבדיקות שמשתמשות בזה בודקות
 * את מה שההורה והתלמידה רואות.
 */
export interface Practice {
  password: string;
  /** ה-API בתור המורה — לבדוק שמה שההורה או התלמידה שלחו הגיע אליה. */
  teacherApi: () => Promise<APIRequestContext>;
  parent: { email: string; name: string };
  student: { email: string; name: string };
  notes: { private: string; forParent: string; forStudent: string };
  resourceTitle: string;
  charge: number;
}

const password = 'Secret123';

const at = (daysFromToday: number, hour: number): string => {
  const d = new Date();
  d.setDate(d.getDate() + daysFromToday);
  d.setHours(hour, 0, 0, 0);
  return d.toISOString();
};

async function post<T>(api: APIRequestContext, path: string, data: unknown): Promise<T> {
  const response = await api.post(path, { data });
  expect(response.ok(), `POST ${path}: ${response.status()} ${await response.text()}`).toBe(true);
  return (await response.json()) as T;
}

/**
 * להורה ולתלמידה יש חשבון שנוצר עם ההוספה שלהן, וההזמנה לקביעת סיסמה נשלחת במייל —
 * שאין לו שרת בבדיקות. לכן הן מקבלות במסד את גיבוב הסיסמה של המורה: הגיבוב של Identity
 * כולל את המלח שלו, ולכן הוא תקף לאותה סיסמה בכל משתמש. קישור ההזמנה עצמו מכוסה בבדיקות
 * ה-backend (AuthFlowTests, ResendInvitationTests).
 */
function sharePassword(fromEmail: string, toEmail: string): void {
  const updated = sql(
    `update "AspNetUsers" u set "PasswordHash" = t."PasswordHash", "EmailConfirmed" = true
     from "AspNetUsers" t
     where t."NormalizedEmail" = upper('${fromEmail}') and u."NormalizedEmail" = upper('${toEmail}')
     returning 1`
  );
  expect(updated, `no account for ${toEmail}`).toBe('1');
}

export async function seedPractice(): Promise<Practice> {
  const run = String(Date.now()).slice(-6);
  const teacherEmail = uniqueEmail('portal-teacher');
  const parent = { email: uniqueEmail('portal-parent'), name: `אבא ${run}` };
  const student = { email: uniqueEmail('portal-student'), name: `נועה ${run}` };
  const notes = {
    private: `הערה פרטית של המורה ${run}`,
    forParent: `הערה להורים בלבד ${run}`,
    forStudent: `הערה לתלמידה ${run}`
  };
  const resourceTitle = `דף תרגול שברים ${run}`;
  const charge = 180;

  const anonymous = await request.newContext({ baseURL: `${environment.apiUrl}/` });
  await post(anonymous, 'auth/register', { email: teacherEmail, password, fullName: `מורה ${run}` });
  confirmEmail(teacherEmail);
  const { accessToken } = await post<{ accessToken: string }>(anonymous, 'auth/login', {
    email: teacherEmail,
    password
  });
  await anonymous.dispose();

  const api = await request.newContext({
    baseURL: `${environment.apiUrl}/`,
    extraHTTPHeaders: { Authorization: `Bearer ${accessToken}` }
  });

  const { id: parentId } = await post<{ id: string }>(api, 'parents', {
    fullName: parent.name,
    gender: 0,
    email: parent.email
  });
  const { id: studentId } = await post<{ id: string }>(api, 'students', {
    fullName: student.name,
    gender: 1,
    loginEmail: student.email,
    parentIds: [parentId]
  });

  // שיעור שהתקיים אתמול ונרשם לו חיוב, ושיעור מחר.
  const past = await post<{ id: string }>(api, 'lessons', { studentId, startTime: at(-1, 10), endTime: at(-1, 11) });
  await post(api, `lessons/${past.id}/complete`, {
    completed: true,
    paymentRequired: true,
    amount: charge,
    noteVisibleToStudent: false,
    noteVisibleToParent: false
  });
  await post(api, 'lessons', { studentId, startTime: at(1, 16), endTime: at(1, 17) });

  await post(api, 'notes', { studentId, content: notes.private, visibleToStudent: false, visibleToParent: false });
  await post(api, 'notes', { studentId, content: notes.forParent, visibleToStudent: false, visibleToParent: true });
  // מסומנת לתלמידה בלבד — השרת מסמן כל הערה שגלויה לתלמידה כגלויה גם להורה.
  await post(api, 'notes', { studentId, content: notes.forStudent, visibleToStudent: true, visibleToParent: false });

  await post(api, `students/${studentId}/resources`, { title: resourceTitle, url: 'https://example.com/fractions' });
  await api.dispose();

  const teacherApi = () =>
    request.newContext({
      baseURL: `${environment.apiUrl}/`,
      extraHTTPHeaders: { Authorization: `Bearer ${accessToken}` }
    });

  sharePassword(teacherEmail, parent.email);
  sharePassword(teacherEmail, student.email);

  return { password, teacherApi, parent, student, notes, resourceTitle, charge };
}
