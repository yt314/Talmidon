import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { stubIntersectionObserver } from '../../../../testing/intersection-observer';
import { stubMatchMedia } from '../../../../testing/match-media';
import { PublicTeacherSummary } from '../public.models';
import { PublicService } from '../public.service';
import { TeacherLibraryComponent } from './teacher-library.component';

const teacher = (id: string, extra: Partial<PublicTeacherSummary> = {}): PublicTeacherSummary => ({
  id, fullName: `מורה ${id}`, bio: null, city: null, neighborhood: null, defaultPricePerLesson: 150,
  subjects: [], photoVersion: null, acceptingStudents: true, ...extra
});

const library = [
  teacher('a', { fullName: 'רחל לוי', city: 'ירושלים', neighborhood: 'רמות', subjects: ['מתמטיקה'], defaultPricePerLesson: 120 }),
  teacher('b', { fullName: 'שרה כהן', city: 'בני ברק', subjects: ['אנגלית', 'מתמטיקה'] }),
  teacher('c', { fullName: 'מרים', city: 'אשדוד', subjects: ['אנגלית'], defaultPricePerLesson: 90 })
];

/** הספרייה הציבורית — הדף הראשון שהורה רואה, בלי התחברות. */
describe('TeacherLibraryComponent', () => {
  let service: { listTeachers: ReturnType<typeof vi.fn>; listSubjects: ReturnType<typeof vi.fn> };

  function create(signedIn = false): TeacherLibraryComponent {
    TestBed.configureTestingModule({
      imports: [TeacherLibraryComponent],
      providers: [
        provideRouter([]),
        { provide: PublicService, useValue: service },
        { provide: AuthService, useValue: { isAuthenticated: () => signedIn, homePath: () => '/app/dashboard' } }
      ]
    });
    const component = TestBed.createComponent(TeacherLibraryComponent).componentInstance;
    component.ngOnInit();
    return component;
  }

  const ids = (component: TeacherLibraryComponent) => component['teachers']().map(t => t.id);

  beforeEach(() => {
    stubMatchMedia();
    stubIntersectionObserver();
    service = { listTeachers: vi.fn(() => of(library)), listSubjects: vi.fn(() => of(['אנגלית', 'מתמטיקה'])) };
  });

  it('בלי סינון — כל המורות, והכותרת הכללית', () => {
    const component = create();
    expect(ids(component)).toEqual(['a', 'b', 'c']);
    expect(component['resultsTitle']()).toBe('המורות שלנו');
    expect(component['loading']()).toBe(false);
  });

  it('החיפוש תופס גם שכונה — הורה מקליד "רמות" בלי לחשוב אם זו עיר', () => {
    const component = create();
    component['search'].set('  רמות ');
    expect(ids(component)).toEqual(['a']);
    expect(component['resultsTitle']()).toBe('נמצאה מורה אחת');
  });

  it('תחום ועיר מצטמצמים יחד, והכותרת סופרת', () => {
    const component = create();
    component['selectedSubject'].set('מתמטיקה');
    expect(component['resultsTitle']()).toBe('נמצאו 2 מורות');

    component['selectedCity'].set('בני ברק');
    expect(ids(component)).toEqual(['b']);
  });

  it('אין תוצאות — נאמר במפורש, וניקוי מחזיר הכול', () => {
    const component = create();
    component['search'].set('אין כזו');
    expect(component['resultsTitle']()).toBe('לא נמצאו מורות');

    component['clearFilters']();

    expect(ids(component)).toEqual(['a', 'b', 'c']);
  });

  it('הערים נגזרות מהמורות שנטענו, ממוינות, בלי כפילויות וריקות', () => {
    service.listTeachers.mockReturnValue(of([...library, teacher('d', { city: 'ירושלים' }), teacher('e')]));
    const component = create();
    expect(component['cities']()).toEqual(['אשדוד', 'בני ברק', 'ירושלים']);
  });

  it('"החל מ־" הוא המחיר הזול ביותר, ואין מחיר כשהספרייה ריקה', () => {
    expect(create()['lowestPrice']()).toBe(90);

    TestBed.resetTestingModule();
    service.listTeachers.mockReturnValue(of([]));
    expect(create()['lowestPrice']()).toBeNull();
  });

  it('מיקום על הכרטיס — "שכונה, עיר", או מה שיש', () => {
    const component = create();
    expect(component['teacherLocation'](library[0])).toBe('רמות, ירושלים');
    expect(component['teacherLocation'](library[1])).toBe('בני ברק');
    expect(component['teacherLocation'](teacher('x'))).toBeNull();
  });

  it('השרת לא זמין — הטעינה נגמרת, בלי ספינר תקוע', () => {
    service.listTeachers.mockReturnValue(throwError(() => new Error('offline')));
    const component = create();
    expect(component['loading']()).toBe(false);
    expect(ids(component)).toEqual([]);
  });

  it('מחוברת — יש לה דרך חזרה לחשבון', () => {
    expect(create(true)['homePath']()).toBe('/app/dashboard');
    TestBed.resetTestingModule();
    expect(create(false)['homePath']()).toBeNull();
  });
});
