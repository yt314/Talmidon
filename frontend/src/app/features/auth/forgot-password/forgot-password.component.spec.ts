import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { stubMatchMedia } from '../../../../testing/match-media';
import { ForgotPasswordComponent } from './forgot-password.component';

describe('ForgotPasswordComponent', () => {
  let auth: { forgotPassword: ReturnType<typeof vi.fn> };
  let component: ForgotPasswordComponent;

  beforeEach(() => {
    stubMatchMedia();
    auth = { forgotPassword: vi.fn(() => of({ message: 'נשלח קישור.' })) };
    TestBed.configureTestingModule({
      imports: [ForgotPasswordComponent],
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }]
    });
    component = TestBed.createComponent(ForgotPasswordComponent).componentInstance;
  });

  it('כתובת לא תקינה — לא נשלח', () => {
    component['form'].setValue({ email: 'not-an-email' });
    component.submit();
    expect(auth.forgotPassword).not.toHaveBeenCalled();
  });

  it('שולח קישור ומציג את ההודעה מהשרת', () => {
    component['form'].setValue({ email: 'dana@example.com' });
    component.submit();
    expect(auth.forgotPassword).toHaveBeenCalledWith('dana@example.com');
    expect(component['successMessage']()).toBe('נשלח קישור.');
  });

  /** כשל ותשובה רגילה נראים אותו דבר — אחרת המסך היה מגלה אילו כתובות רשומות. */
  it('גם בכשל מוצגת הודעה כללית, לא שגיאה', () => {
    auth.forgotPassword.mockReturnValue(throwError(() => new Error('404')));
    component['form'].setValue({ email: 'nobody@example.com' });
    component.submit();
    expect(component['done']()).toBe(true);
    expect(component['successMessage']()).toContain('אם הכתובת רשומה במערכת');
  });
});
