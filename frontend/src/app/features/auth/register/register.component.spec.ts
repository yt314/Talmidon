import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { stubMatchMedia } from '../../../../testing/match-media';
import { RegisterComponent } from './register.component';

/** הרשמה של מורה חדשה — הצעד הראשון במוצר. */
describe('RegisterComponent', () => {
  let auth: { register: ReturnType<typeof vi.fn> };
  let component: RegisterComponent;

  beforeEach(() => {
    stubMatchMedia();
    auth = { register: vi.fn(() => of({ message: 'נשלח מייל לאימות.' })) };
    TestBed.configureTestingModule({
      imports: [RegisterComponent],
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }]
    });
    component = TestBed.createComponent(RegisterComponent).componentInstance;
  });

  const fill = (extra: Partial<Record<'fullName' | 'email' | 'password' | 'confirmPassword' | 'phone', string>> = {}) =>
    component['form'].setValue({
      fullName: 'דנה כהן',
      email: 'dana@example.com',
      password: 'Secret123',
      confirmPassword: 'Secret123',
      phone: '',
      ...extra
    });

  it('נרשמת, ורואה את ההודעה מהשרת', () => {
    fill();
    component.submit();
    expect(auth.register).toHaveBeenCalledWith({ fullName: 'דנה כהן', email: 'dana@example.com', password: 'Secret123', phone: undefined });
    expect(component['done']()).toBe(true);
    expect(component['successMessage']()).toBe('נשלח מייל לאימות.');
  });

  it('טלפון שמולא נשלח', () => {
    fill({ phone: '050-1234567' });
    component.submit();
    expect(auth.register).toHaveBeenCalledWith(expect.objectContaining({ phone: '050-1234567' }));
  });

  it('סיסמאות שונות — לא נשלח', () => {
    fill({ confirmPassword: 'Secret124' });
    component.submit();
    expect(auth.register).not.toHaveBeenCalled();
  });

  it('סיסמה שהשרת היה דוחה — לא נשלח', () => {
    fill({ password: 'secret123', confirmPassword: 'secret123' });
    component.submit();
    expect(auth.register).not.toHaveBeenCalled();
  });

  it('כשל — ההודעה מהשרת מוצגת, והטופס נשאר', () => {
    auth.register.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 400, error: { message: 'ההרשמה נכשלה.' } })));
    fill();
    component.submit();
    expect(component['error']()).toBe('ההרשמה נכשלה.');
    expect(component['done']()).toBe(false);
    expect(component['loading']()).toBe(false);
  });
});
