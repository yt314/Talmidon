import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { stubMatchMedia } from '../../../../testing/match-media';
import { LoginComponent } from './login.component';

const failure = (message: string) => new HttpErrorResponse({ status: 400, error: { message } });

/** מסך ההתחברות: לאן נכנסים, ומה מוסבר כשמשהו לא הסתדר. */
describe('LoginComponent', () => {
  let auth: { login: ReturnType<typeof vi.fn>; resendConfirmation: ReturnType<typeof vi.fn>; homePath: () => string };
  let navigateByUrl: ReturnType<typeof vi.fn>;

  function create(queryParams: Record<string, string> = {}): LoginComponent {
    TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: auth },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } } }
      ]
    });
    navigateByUrl = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true) as never;
    const component = TestBed.createComponent(LoginComponent).componentInstance;
    component.ngOnInit();
    return component;
  }

  const fill = (component: LoginComponent, email = 'dana@example.com', password = 'Secret123') =>
    component['form'].setValue({ email, password });

  beforeEach(() => {
    stubMatchMedia();
    auth = {
      login: vi.fn(() => of({})),
      resendConfirmation: vi.fn(() => of({ message: 'ok' })),
      homePath: () => '/app/dashboard'
    };
  });

  it('טופס לא תקין לא נשלח', () => {
    const component = create();
    component.submit();
    expect(auth.login).not.toHaveBeenCalled();
    expect(component['form'].controls.email.touched).toBe(true);
  });

  it('אחרי התחברות — לעמוד הבית לפי התפקיד', () => {
    const component = create();
    fill(component);
    component.submit();
    expect(auth.login).toHaveBeenCalledWith({ email: 'dana@example.com', password: 'Secret123' });
    expect(navigateByUrl).toHaveBeenCalledWith('/app/dashboard');
  });

  it('אחרי התחברות — חזרה לכתובת שממנה הגיעה', () => {
    const component = create({ returnUrl: '/app/contact-requests?id=7' });
    fill(component);
    component.submit();
    expect(navigateByUrl).toHaveBeenCalledWith('/app/contact-requests?id=7');
  });

  /** returnUrl לאתר אחר הוא הדרך הקלאסית לנצל מסך התחברות אמיתי להעברה לאתר מתחזה. */
  it.each(['https://evil.example/login', '//evil.example'])('returnUrl חיצוני (%s) מתעלמים ממנו', returnUrl => {
    const component = create({ returnUrl });
    fill(component);
    component.submit();
    expect(navigateByUrl).toHaveBeenCalledWith('/app/dashboard');
  });

  it('כשל — ההודעה מהשרת מוצגת, והכפתור חוזר לפעול', () => {
    auth.login.mockReturnValue(throwError(() => failure('אימייל או סיסמה שגויים.')));
    const component = create();
    fill(component);
    component.submit();
    expect(component['error']()).toBe('אימייל או סיסמה שגויים.');
    expect(component['loading']()).toBe(false);
    expect(component['unconfirmedEmail']()).toBeNull();
  });

  it('מייל שלא אומת — מציעים לשלוח קישור אימות חדש לאותה כתובת', () => {
    auth.login.mockReturnValue(throwError(() => failure('יש לאמת את כתובת המייל לפני התחברות.')));
    const component = create();
    fill(component);
    component.submit();
    expect(component['unconfirmedEmail']()).toBe('dana@example.com');

    component.resendConfirmation();

    expect(auth.resendConfirmation).toHaveBeenCalledWith('dana@example.com');
    expect(component['resendDone']()).toBe(true);
  });

  /** תשובה זהה גם בכשל, כדי לא לחשוף אם המייל רשום. */
  it('שליחה חוזרת שנכשלה מוצגת כמו שליחה שהצליחה', () => {
    auth.resendConfirmation.mockReturnValue(throwError(() => failure('x')));
    const component = create({ confirmed: '0' });
    fill(component);
    component.resendConfirmation();
    expect(component['resendDone']()).toBe(true);
  });

  it('קישור אימות שפג — שליחה חוזרת דורשת כתובת בטופס', () => {
    const component = create({ confirmed: '0' });
    component.resendConfirmation();
    expect(auth.resendConfirmation).not.toHaveBeenCalled();
    expect(component['error']()).toContain('כתובת המייל');
  });

  it.each([
    ['confirmed', '1', 'confirmed'],
    ['confirmed', '0', 'confirmFailed'],
    ['passwordChanged', '1', 'passwordChanged'],
    ['timeout', '1', 'timedOut'],
    ['expired', '1', 'sessionExpired']
  ] as const)('?%s=%s מציג את ההסבר המתאים', (param, value, flag) => {
    const component = create({ [param]: value });
    expect(component[flag]()).toBe(true);
  });
});
