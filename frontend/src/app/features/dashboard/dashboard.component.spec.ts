import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { stubIntersectionObserver } from '../../../testing/intersection-observer';
import { stubMatchMedia } from '../../../testing/match-media';
import { ContactRequestsService } from '../contact-requests/contact-requests.service';
import { Lesson, LessonStatus } from '../lessons/lessons.models';
import { LessonsService } from '../lessons/lessons.service';
import { PaymentsService } from '../payments/payments.service';
import { StudentsService } from '../students/students.service';
import { ProfileSetupService } from '../teacher/profile-setup/profile-setup.service';
import { DashboardComponent } from './dashboard.component';

const lesson = (id: string, endTime: string): Lesson => ({ id, endTime, startTime: endTime }) as unknown as Lesson;
const offline = () => throwError(() => new Error('offline'));

/** לוח הבקרה — המסך הראשון אחרי ההתחברות. */
describe('DashboardComponent', () => {
  let lessons: { list: ReturnType<typeof vi.fn>; listChangeRequests: ReturnType<typeof vi.fn> };
  let students: { list: ReturnType<typeof vi.fn> };
  let profile: { load: ReturnType<typeof vi.fn> };
  let payments: { openCharges: ReturnType<typeof vi.fn> };
  let contacts: { newCount: ReturnType<typeof vi.fn> };

  /** אותה קריאה לשיעורים משרתת ארבע שאלות; התשובה נבחרת לפי הפרמטרים. */
  let lessonsBy: {
    all: () => Observable<Lesson[]>;
    today: () => Observable<Lesson[]>;
    requested: () => Observable<Lesson[]>;
    toMark: () => Observable<Lesson[]>;
  };

  function create(): DashboardComponent {
    TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        provideRouter([]),
        { provide: LessonsService, useValue: lessons },
        { provide: StudentsService, useValue: students },
        { provide: ProfileSetupService, useValue: profile },
        { provide: PaymentsService, useValue: payments },
        { provide: ContactRequestsService, useValue: contacts }
      ]
    });
    const component = TestBed.createComponent(DashboardComponent).componentInstance;
    component.ngOnInit();
    return component;
  }

  beforeEach(() => {
    stubMatchMedia();
    stubIntersectionObserver();
    lessonsBy = { all: () => of([]), today: () => of([]), requested: () => of([]), toMark: () => of([]) };
    lessons = {
      list: vi.fn((from?: Date, to?: Date, status?: LessonStatus) => {
        if (status === LessonStatus.Requested) return lessonsBy.requested();
        if (status === LessonStatus.Scheduled) return lessonsBy.toMark();
        return from ? lessonsBy.today() : lessonsBy.all();
      }),
      listChangeRequests: vi.fn(() => of([{}, {}]))
    };
    students = { list: vi.fn(() => of([{}])) };
    profile = { load: vi.fn(() => of({ isProfileComplete: true })) };
    payments = { openCharges: vi.fn(() => of([{ amount: 150 }, { amount: 90.5 }])) };
    contacts = { newCount: vi.fn(() => of(3)) };
  });

  it('היום של המורה במשפט אחד', () => {
    lessonsBy.today = () => of([lesson('a', '2026-03-01T10:00:00Z'), lesson('b', '2026-03-01T11:00:00Z')]);
    expect(create()['todaySummary']()).toBe('2 שיעורים מתוזמנים להיום.');
  });

  it.each([
    [0, 'אין שיעורים מתוזמנים להיום.'],
    [1, 'שיעור אחד מתוזמן להיום.']
  ])('%i שיעורים: "%s"', (count, summary) => {
    lessonsBy.today = () => of(Array.from({ length: count }, (_, i) => lesson(`${i}`, '2026-03-01T10:00:00Z')));
    expect(create()['todaySummary']()).toBe(summary);
  });

  it('היום נשאל מתחילת היום ועד סופו', () => {
    create();
    const [from, to] = lessons.list.mock.calls.find(([f]) => f instanceof Date)!;
    expect([from.getHours(), from.getMinutes()]).toEqual([0, 0]);
    expect([to.getHours(), to.getMinutes(), to.getMilliseconds()]).toEqual([23, 59, 999]);
  });

  it('בקשות ממתינות — שיעורים חדשים ובקשות שינוי יחד', () => {
    lessonsBy.requested = () => of([lesson('r', '2026-03-01T10:00:00Z')]);
    expect(create()['pendingRequestsTotal']()).toBe(3);
  });

  it('חיובים פתוחים — מספר וסכום', () => {
    const component = create();
    expect(component['openChargesCount']()).toBe(2);
    expect(component['openChargesTotal']()).toBe(240.5);
    expect(component['newContactRequests']()).toBe(3);
  });

  it('ממתינים לסימון — רק שיעורים שכבר נגמרו', () => {
    lessonsBy.toMark = () =>
      of([lesson('past', '2000-01-01T10:00:00Z'), lesson('future', '2999-01-01T10:00:00Z')]);
    expect(create()['lessonsToMark']()).toBe(1);
  });

  it('חשבון חדש — שלושת הצעדים, וכמה מהם הושלמו', () => {
    students.list.mockReturnValue(of([]));
    const component = create();
    expect(component['onboarding']()!.map(s => s.done)).toEqual([true, false, false]);
    expect(component['onboardingDone']()).toBe(1);
  });

  it('מורה ותיקה — בלי הדרכה', () => {
    lessonsBy.all = () => of([lesson('a', '2026-03-01T10:00:00Z')]);
    expect(create()['onboarding']()).toBeNull();
  });

  /** כשל בטעינת הפרופיל אינו אמור להציג "הפרופיל לא מלא" למי שכן מילאה אותו. */
  it('השרת לא זמין — כל כרטיס נגמר באפס, והפרופיל לא מוצג כחסר', () => {
    students.list.mockReturnValue(offline());
    profile.load.mockReturnValue(offline());
    lessonsBy = { all: offline, today: offline, requested: offline, toMark: offline };
    lessons.listChangeRequests.mockReturnValue(offline());
    payments.openCharges.mockReturnValue(offline());
    contacts.newCount.mockReturnValue(offline());

    const component = create();

    expect(component['todaySummary']()).toBe('אין שיעורים מתוזמנים להיום.');
    expect(component['pendingRequestsTotal']()).toBe(0);
    expect(component['openChargesCount']()).toBe(0);
    expect(component['lessonsToMark']()).toBe(0);
    expect(component['newContactRequests']()).toBe(0);
    expect(component['onboarding']()!.find(s => s.link === '/app/profile')!.done).toBe(true);
  });
});
