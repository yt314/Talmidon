import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { stubMatchMedia } from '../../../../testing/match-media';
import { ContactRequestsService } from '../../contact-requests/contact-requests.service';
import { PublicTeacherDetail } from '../public.models';
import { PublicService } from '../public.service';
import { TeacherProfileComponent } from './teacher-profile.component';

const detail = (extra: Partial<PublicTeacherDetail> = {}): PublicTeacherDetail => ({
  id: 't1', fullName: 'רחל לוי', bio: null, city: 'ירושלים', neighborhood: 'רמות', phone: null, contactEmail: null,
  defaultPricePerLesson: 150, rulesText: null, subjects: ['מתמטיקה'], photoVersion: null, acceptingStudents: true, ...extra
});

/** דף המורה בספרייה — הקישור שהורים שולחים זה לזה. */
describe('TeacherProfileComponent', () => {
  let service: { getTeacher: ReturnType<typeof vi.fn> };

  function create(id: string | null = 't1'): TeacherProfileComponent {
    TestBed.configureTestingModule({
      imports: [TeacherProfileComponent],
      providers: [
        provideRouter([]),
        { provide: PublicService, useValue: service },
        { provide: ContactRequestsService, useValue: { send: vi.fn() } },
        { provide: AuthService, useValue: { isAuthenticated: () => false, homePath: () => '/login' } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap(id ? { id } : {}) } } }
      ]
    });
    const component = TestBed.createComponent(TeacherProfileComponent).componentInstance;
    component.ngOnInit();
    return component;
  }

  beforeEach(() => {
    stubMatchMedia();
    service = { getTeacher: vi.fn(() => of(detail())) };
  });

  it('טוען את המורה מהכתובת', () => {
    const component = create();
    expect(service.getTeacher).toHaveBeenCalledWith('t1');
    expect(component['teacher']()?.fullName).toBe('רחל לוי');
    expect(component['location']()).toBe('רמות, ירושלים');
  });

  it('בלי פרטי קשר — אין מה להציג תחת "יצירת קשר"', () => {
    expect(create()['hasContactDetails']()).toBe(false);
  });

  it.each([{ phone: '050' }, { contactEmail: 'r@example.com' }])('%j מספיק כדי להציג פרטי קשר', extra => {
    service.getTeacher.mockReturnValue(of(detail(extra)));
    expect(create()['hasContactDetails']()).toBe(true);
  });

  /** קישור ישן למורה שהסירה את הפרופיל — הדף מציג "לא נמצא", לא ספינר אינסופי. */
  it('מורה שלא קיימת — הטעינה נגמרת בלי מורה', () => {
    service.getTeacher.mockReturnValue(throwError(() => new Error('404')));
    const component = create();
    expect(component['loading']()).toBe(false);
    expect(component['teacher']()).toBeNull();
  });

  it('כתובת בלי מזהה — לא פונה לשרת', () => {
    const component = create(null);
    expect(service.getTeacher).not.toHaveBeenCalled();
    expect(component['loading']()).toBe(false);
  });
});
