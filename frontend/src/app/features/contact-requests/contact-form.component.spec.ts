import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ContactFormComponent } from './contact-form.component';
import { ContactRequestsService } from './contact-requests.service';

/**
 * טופס הפנייה בדף המורה. הפונה אינו מחובר, כך שכל תקלה מגיעה אליו ולא ליומן שגיאות —
 * ולכן כל מצב צריך הסבר משלו.
 */
describe('ContactFormComponent', () => {
  let service: { send: ReturnType<typeof vi.fn> };
  let component: ContactFormComponent;

  beforeEach(() => {
    service = { send: vi.fn(() => of(undefined)) };
    TestBed.configureTestingModule({
      imports: [ContactFormComponent],
      providers: [{ provide: ContactRequestsService, useValue: service }]
    });
    const fixture = TestBed.createComponent(ContactFormComponent);
    fixture.componentRef.setInput('teacherId', 't1');
    component = fixture.componentInstance;
  });

  const fill = (extra: Partial<Record<'fullName' | 'phone' | 'email' | 'message', string>> = {}) =>
    component['form'].patchValue({ fullName: 'אמא של דנה', phone: '050-1234567', message: 'מחפשת מורה למתמטיקה', ...extra });

  const failWith = (status: number, error: unknown = null) => {
    service.send.mockReturnValue(throwError(() => new HttpErrorResponse({ status, error })));
    fill();
    component['submit']();
    return component['error']();
  };

  it('שם, טלפון והודעה הם חובה', () => {
    component['submit']();
    expect(service.send).not.toHaveBeenCalled();
    expect(component['form'].controls.phone.touched).toBe(true);
  });

  it('נשלחת למורה הנכונה; מייל ריק נשלח כ-null', () => {
    fill();
    component['form'].controls.subject.setValue('מתמטיקה');
    component['submit']();
    expect(service.send).toHaveBeenCalledWith('t1', {
      fullName: 'אמא של דנה', phone: '050-1234567', email: null, subject: 'מתמטיקה', message: 'מחפשת מורה למתמטיקה'
    });
    expect(component['sent']()).toBe(true);
    expect(component['sending']()).toBe(false);
  });

  it('מייל לא תקין — לא נשלח', () => {
    fill({ email: 'not-an-email' });
    component['submit']();
    expect(service.send).not.toHaveBeenCalled();
  });

  it('השרת לא נגיש (0) — לבדוק חיבור ולנסות שוב', () => {
    expect(failWith(0)).toContain('לא הצלחנו להגיע לשרת');
  });

  it('404 — הפרופיל לא מקבל פניות', () => {
    expect(failWith(404)).toContain('לא זמין כרגע');
  });

  it('429 — יותר מדי פניות, לנסות בעוד כמה דקות', () => {
    expect(failWith(429)).toContain('יותר מדי פניות');
  });

  it('400 — ההודעה מהשרת על השדה שאינו תקין', () => {
    expect(failWith(400, { message: 'מספר הטלפון אינו תקין.' })).toBe('מספר הטלפון אינו תקין.');
  });

  it('כל שגיאה אחרת — עם מספר השגיאה, והפניה לפרטים שבעמוד', () => {
    const message = failWith(500);
    expect(message).toContain('500');
    expect(message).toContain('ישירות');
    expect(component['sent']()).toBe(false);
    expect(component['sending']()).toBe(false);
  });
});
