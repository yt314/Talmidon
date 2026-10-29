import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { environment } from '../../../environments/environment';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

const api = environment.apiUrl;
const unauthorized = { status: 401, statusText: 'Unauthorized' };

/**
 * אסימון הגישה חי רבע שעה, ולכן הרענון קורה כל הזמן באמצע עבודה. כשהוא נשבר, המורה
 * נזרקת החוצה באמצע סימון שיעור, או שכל מסך מתמלא בשגיאות.
 */
describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let router: Router;
  let auth: {
    accessToken: ReturnType<typeof vi.fn>;
    refreshTokenValue: ReturnType<typeof vi.fn>;
    refresh: ReturnType<typeof vi.fn>;
    clearSession: ReturnType<typeof vi.fn>;
  };

  beforeEach(() => {
    auth = {
      accessToken: vi.fn(() => 'old-token'),
      refreshTokenValue: vi.fn(() => 'refresh-token'),
      refresh: vi.fn(),
      clearSession: vi.fn()
    };
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AuthService, useValue: auth }
      ]
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
  });

  afterEach(() => backend.verify());

  /** הרענון כבקשה אמיתית לכתובת שלו, כדי שאפשר יהיה לבדוק כמה פעמים נקרא ומתי הוא עונה. */
  const refreshThroughBackend = () =>
    auth.refresh.mockImplementation(() => TestBed.inject(HttpClient).post<string>(`${api}/auth/refresh`, null));

  it('מצרף את אסימון הגישה לבקשות רגילות', () => {
    http.get(`${api}/students`).subscribe();
    expect(backend.expectOne(`${api}/students`).request.headers.get('Authorization')).toBe('Bearer old-token');
  });

  it('לא מצרף אסימון לקריאות ההתחברות עצמן', () => {
    http.post(`${api}/auth/login`, {}).subscribe();
    expect(backend.expectOne(`${api}/auth/login`).request.headers.has('Authorization')).toBe(false);
  });

  it('על 401 מרענן ושולח את הבקשה שוב עם האסימון החדש — והמסך מקבל את התשובה', () => {
    refreshThroughBackend();
    let result: unknown;

    http.get(`${api}/students`).subscribe(body => (result = body));
    backend.expectOne(`${api}/students`).flush(null, unauthorized);
    backend.expectOne(`${api}/auth/refresh`).flush('new-token');
    const retry = backend.expectOne(`${api}/students`);
    retry.flush([{ id: 's1' }]);

    expect(retry.request.headers.get('Authorization')).toBe('Bearer new-token');
    expect(result).toEqual([{ id: 's1' }]);
  });

  /** לוח הבקרה שולח כמה בקשות יחד. שני רענונים היו שורפים את אסימון הרענון: השרת מזהה שימוש חוזר ומבטל את כל המשפחה. */
  it('שתי בקשות שנכשלות יחד מפעילות רענון אחד, ושתיהן נשלחות שוב', () => {
    refreshThroughBackend();
    const results: unknown[] = [];

    http.get(`${api}/students`).subscribe(body => results.push(body));
    http.get(`${api}/lessons`).subscribe(body => results.push(body));
    backend.expectOne(`${api}/students`).flush(null, unauthorized);
    backend.expectOne(`${api}/lessons`).flush(null, unauthorized);

    backend.expectOne(`${api}/auth/refresh`).flush('new-token');
    for (const url of [`${api}/students`, `${api}/lessons`]) {
      const retry = backend.expectOne(url);
      expect(retry.request.headers.get('Authorization')).toBe('Bearer new-token');
      retry.flush('ok');
    }

    expect(auth.refresh).toHaveBeenCalledTimes(1);
    expect(results).toEqual(['ok', 'ok']);
  });

  it('רענון שנכשל מנקה את ההפעלה ושולח להתחברות עם הסבר והכתובת לחזרה', async () => {
    refreshThroughBackend();
    await router.navigateByUrl('/');
    vi.spyOn(router, 'url', 'get').mockReturnValue('/app/students/s1');
    let failed: HttpErrorResponse | undefined;

    http.get(`${api}/students`).subscribe({ error: e => (failed = e) });
    backend.expectOne(`${api}/students`).flush(null, unauthorized);
    backend.expectOne(`${api}/auth/refresh`).flush(null, unauthorized);

    expect(auth.clearSession).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/login'], { queryParams: { expired: 1, returnUrl: '/app/students/s1' } });
    expect(failed?.status).toBe(401);
  });

  /** בקשה שהמתינה לרענון שנכשל מקבלת שגיאה — אחרת הספינר שלה נשאר על המסך לתמיד. */
  it('רענון שנכשל מחזיר שגיאה לכל הבקשות שהמתינו לו, ומפנה להתחברות פעם אחת', () => {
    refreshThroughBackend();
    const failures: number[] = [];

    http.get(`${api}/students`).subscribe({ error: (e: HttpErrorResponse) => failures.push(e.status) });
    http.get(`${api}/lessons`).subscribe({ error: (e: HttpErrorResponse) => failures.push(e.status) });
    backend.expectOne(`${api}/students`).flush(null, unauthorized);
    backend.expectOne(`${api}/lessons`).flush(null, unauthorized);
    backend.expectOne(`${api}/auth/refresh`).flush(null, unauthorized);

    expect(failures).toEqual([401, 401]);
    expect(router.navigate).toHaveBeenCalledTimes(1);
  });

  /** "//host" היא כתובת לאתר אחר; ו-/login עצמו אינו יעד לחזור אליו. */
  it.each(['/login?expired=1', '//evil.example'])('לא זוכר את %s ככתובת לחזרה', url => {
    refreshThroughBackend();
    vi.spyOn(router, 'url', 'get').mockReturnValue(url);

    http.get(`${api}/students`).subscribe({ error: () => undefined });
    backend.expectOne(`${api}/students`).flush(null, unauthorized);
    backend.expectOne(`${api}/auth/refresh`).flush(null, unauthorized);

    expect(router.navigate).toHaveBeenCalledWith(['/login'], { queryParams: { expired: 1 } });
  });

  it('סיסמה שגויה בהתחברות (401) אינה מפעילה רענון', () => {
    let failed: HttpErrorResponse | undefined;

    http.post(`${api}/auth/login`, {}).subscribe({ error: e => (failed = e) });
    backend.expectOne(`${api}/auth/login`).flush(null, unauthorized);

    expect(auth.refresh).not.toHaveBeenCalled();
    expect(failed?.status).toBe(401);
  });

  it('בלי אסימון רענון — השגיאה עוברת כמו שהיא', () => {
    auth.refreshTokenValue.mockReturnValue(null);
    let failed: HttpErrorResponse | undefined;

    http.get(`${api}/students`).subscribe({ error: e => (failed = e) });
    backend.expectOne(`${api}/students`).flush(null, unauthorized);

    expect(auth.refresh).not.toHaveBeenCalled();
    expect(failed?.status).toBe(401);
  });

  it('שגיאות אחרות לא נוגעות בהפעלה', () => {
    let failed: HttpErrorResponse | undefined;

    http.get(`${api}/students`).subscribe({ error: e => (failed = e) });
    backend.expectOne(`${api}/students`).flush(null, { status: 500, statusText: 'Server Error' });

    expect(auth.refresh).not.toHaveBeenCalled();
    expect(auth.clearSession).not.toHaveBeenCalled();
    expect(failed?.status).toBe(500);
  });
});

describe('authInterceptor — בקשה שבוטלה באמצע רענון', () => {
  /**
   * מונה ההתראות מחליף בקשה בכל דקה ובכל מעבר לשונית. כשהרענון היה תלוי בבקשה שהתחילה
   * אותו, ביטול שלה השאיר אותו "רץ" לנצח, וכל בקשה שפקעה אחר כך חיכתה עד רענון הדף.
   */
  it('הרענון ממשיך ומסתיים, והבקשה הבאה עובדת', () => {
    let token = 'old-token';
    const auth = {
      accessToken: vi.fn(() => token),
      refreshTokenValue: vi.fn(() => 'refresh-token'),
      refresh: vi.fn(() => TestBed.inject(HttpClient).post<string>(`${api}/auth/refresh`, null)),
      clearSession: vi.fn()
    };
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AuthService, useValue: auth }
      ]
    });
    const http = TestBed.inject(HttpClient);
    const backend = TestBed.inject(HttpTestingController);

    const poll = http.get(`${api}/notifications/unread-count`).subscribe();
    backend.expectOne(`${api}/notifications/unread-count`).flush(null, unauthorized);
    const refresh = backend.expectOne(`${api}/auth/refresh`);
    poll.unsubscribe();

    expect(refresh.cancelled).toBe(false);
    refresh.flush('new-token');
    token = 'new-token';

    let result: unknown;
    http.get(`${api}/students`).subscribe(body => (result = body));
    const next = backend.expectOne(`${api}/students`);
    next.flush(['ok']);

    expect(next.request.headers.get('Authorization')).toBe('Bearer new-token');
    expect(result).toEqual(['ok']);
    backend.verify();
  });

  it('גם כשהרענון שבוטל עוד לא חזר — בקשה חדשה שפקעה מחכה לו ולא נתקעת', () => {
    const auth = {
      accessToken: vi.fn(() => 'old-token'),
      refreshTokenValue: vi.fn(() => 'refresh-token'),
      refresh: vi.fn(() => TestBed.inject(HttpClient).post<string>(`${api}/auth/refresh`, null)),
      clearSession: vi.fn()
    };
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AuthService, useValue: auth }
      ]
    });
    const http = TestBed.inject(HttpClient);
    const backend = TestBed.inject(HttpTestingController);

    const poll = http.get(`${api}/notifications/unread-count`).subscribe();
    backend.expectOne(`${api}/notifications/unread-count`).flush(null, unauthorized);
    const refresh = backend.expectOne(`${api}/auth/refresh`);
    poll.unsubscribe();

    let result: unknown;
    http.get(`${api}/students`).subscribe(body => (result = body));
    backend.expectOne(`${api}/students`).flush(null, unauthorized);
    refresh.flush('new-token');
    backend.expectOne(`${api}/students`).flush(['ok']);

    expect(auth.refresh).toHaveBeenCalledTimes(1);
    expect(result).toEqual(['ok']);
    backend.verify();
  });
});
