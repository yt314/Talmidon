import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { environment } from '../../../environments/environment';
import { AuthResponse, Role } from './auth.models';
import { AuthService } from './auth.service';

const STORAGE_KEY = 'talmidon_session';
const api = `${environment.apiUrl}/auth`;

const response = (roles: Role[], accessToken = 'access-1'): AuthResponse =>
  ({ accessToken, refreshToken: 'refresh-1', email: 'dana@example.com', roles }) as AuthResponse;

/** ההפעלה של המשתמש: מה נשמר, מה נטען בפתיחה מחדש, ולאן כל תפקיד נכנס. */
describe('AuthService', () => {
  let http: HttpTestingController;

  const create = () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    return TestBed.inject(AuthService);
  };

  const signIn = (auth: AuthService, roles: Role[]) => {
    auth.login({ email: 'dana@example.com', password: 'pw' }).subscribe();
    http.expectOne(`${api}/login`).flush(response(roles));
  };

  beforeEach(() => localStorage.clear());
  afterEach(() => http.verify());

  it('התחברות שומרת את ההפעלה, גם לטעינה הבאה של האתר', () => {
    const auth = create();

    signIn(auth, ['Teacher']);

    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.accessToken()).toBe('access-1');
    expect(JSON.parse(localStorage.getItem(STORAGE_KEY)!)).toMatchObject({ refreshToken: 'refresh-1', roles: ['Teacher'] });
  });

  it('הפעלה שמורה נטענת בפתיחת האתר', () => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(response(['Parent'])));

    const auth = create();

    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.currentEmail()).toBe('dana@example.com');
  });

  it('הפעלה שמורה פגומה לא מפילה את האתר — פשוט לא מחוברים', () => {
    localStorage.setItem(STORAGE_KEY, '{not json');

    const auth = create();

    expect(auth.isAuthenticated()).toBe(false);
  });

  it.each([
    [['Admin', 'Teacher'], '/admin'],
    [['Teacher'], '/app/dashboard'],
    [['Parent'], '/parent'],
    [['Student'], '/student'],
    [[], '/login']
  ] as [Role[], string][])('תפקידים %j נכנסים אל %s', (roles, home) => {
    const auth = create();
    signIn(auth, roles);
    expect(auth.homePath()).toBe(home);
  });

  it('רענון שולח את אסימון הרענון ושומר את האסימונים החדשים', () => {
    const auth = create();
    signIn(auth, ['Teacher']);
    let fresh: string | undefined;

    auth.refresh().subscribe(token => (fresh = token));
    const request = http.expectOne(`${api}/refresh`);
    request.flush(response(['Teacher'], 'access-2'));

    expect(request.request.body).toEqual({ refreshToken: 'refresh-1' });
    expect(fresh).toBe('access-2');
    expect(auth.accessToken()).toBe('access-2');
  });

  it('התנתקות מבטלת את אסימון הרענון בשרת ומוחקת את ההפעלה מיד', () => {
    const auth = create();
    signIn(auth, ['Teacher']);

    auth.logout();

    expect(auth.isAuthenticated()).toBe(false);
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
    expect(http.expectOne(`${api}/logout`).request.body).toEqual({ refreshToken: 'refresh-1' });
  });

  /** השרת לא זמין ברגע ההתנתקות — היא עדיין מנותקת במכשיר, וזה מה שחשוב. */
  it('כשל בקריאת ההתנתקות לא מחזיר את ההפעלה', () => {
    const auth = create();
    signIn(auth, ['Teacher']);

    auth.logout();
    http.expectOne(`${api}/logout`).flush(null, { status: 500, statusText: 'Server Error' });

    expect(auth.isAuthenticated()).toBe(false);
  });

  it('התנתקות בלי הפעלה לא פונה לשרת', () => {
    const auth = create();
    auth.logout();
    http.expectNone(`${api}/logout`);
  });
});
