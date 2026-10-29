import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { stubMatchMedia } from '../../../../testing/match-media';
import { SetPasswordComponent } from './set-password.component';

/** קביעת סיסמה מקישור במייל — גם להזמנת הורה ותלמיד, וגם לאיפוס סיסמה. */
describe('SetPasswordComponent', () => {
  let auth: { setPassword: ReturnType<typeof vi.fn> };

  function create(queryParams: Record<string, string>): SetPasswordComponent {
    TestBed.configureTestingModule({
      imports: [SetPasswordComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: auth },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } } }
      ]
    });
    const component = TestBed.createComponent(SetPasswordComponent).componentInstance;
    component.ngOnInit();
    return component;
  }

  beforeEach(() => {
    stubMatchMedia();
    auth = { setPassword: vi.fn(() => of({ message: 'ok' })) };
  });

  it.each<Record<string, string>>([{}, { userId: 'u1' }, { token: 't' }])('קישור בלי מזהה ואסימון (%j) מוצג כלא תקין', params => {
    expect(create(params)['linkInvalid']()).toBe(true);
  });

  it('שולח את המזהה והאסימון מהקישור יחד עם הסיסמה', () => {
    const component = create({ userId: 'u1', token: 'tok' });
    component['form'].setValue({ password: 'Secret123', confirmPassword: 'Secret123' });
    component.submit();
    expect(auth.setPassword).toHaveBeenCalledWith({ userId: 'u1', token: 'tok', password: 'Secret123' });
    expect(component['done']()).toBe(true);
  });

  it('סיסמאות שונות — לא נשלח', () => {
    const component = create({ userId: 'u1', token: 'tok' });
    component['form'].setValue({ password: 'Secret123', confirmPassword: 'Secret12' });
    component.submit();
    expect(auth.setPassword).not.toHaveBeenCalled();
  });

  it('קישור שפג — ההסבר מוצג', () => {
    auth.setPassword.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 400, error: {} })));
    const component = create({ userId: 'u1', token: 'tok' });
    component['form'].setValue({ password: 'Secret123', confirmPassword: 'Secret123' });
    component.submit();
    expect(component['error']()).toContain('ייתכן שהקישור פג תוקף');
    expect(component['done']()).toBe(false);
  });
});
