import { Component, input, output } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { LessonCalendarComponent } from '../../../shared/calendar/lesson-calendar.component';
import { ChangeRequest, ChangeRequestStatus, ChangeRequestType, Lesson } from '../../lessons/lessons.models';
import { ParentPortalService } from '../parent-portal.service';
import { ParentLessonsComponent } from './parent-lessons.component';

@Component({ selector: 'app-lesson-calendar', template: '' })
class CalendarStub {
  readonly events = input<unknown[]>([]);
  readonly selectable = input(false);
  readonly eventClicked = output<unknown>();
}

const at = (day: number, hour: number) => new Date(2026, 2, day, hour);
const lesson = { id: 'l1', studentId: 'kid1', startTime: at(10, 16).toISOString(), endTime: at(10, 17).toISOString() } as Lesson;

/** יומן ההורה: בקשת שיעור חדש, ובקשת ביטול או שינוי מועד לשיעור קיים. */
describe('ParentLessonsComponent', () => {
  let portal: Record<'myChildren' | 'myLessons' | 'requestLesson' | 'requestChange' | 'myChangeRequests', ReturnType<typeof vi.fn>>;

  function create(queryParams: Record<string, string> = {}): ParentLessonsComponent {
    TestBed.configureTestingModule({
      imports: [ParentLessonsComponent],
      providers: [
        MessageService,
        { provide: ParentPortalService, useValue: portal },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } } }
      ]
    });
    // היומן עצמו (FullCalendar) אינו נושא הבדיקה, ואינו נבנה ונהרס כראוי ב-jsdom
    TestBed.overrideComponent(ParentLessonsComponent, {
      remove: { imports: [LessonCalendarComponent] },
      add: { imports: [CalendarStub] }
    });
    const component = TestBed.createComponent(ParentLessonsComponent).componentInstance;
    component.ngOnInit();
    return component;
  }

  beforeEach(() => {
    portal = {
      myChildren: vi.fn(() => of([{ id: 'kid1', fullName: 'נועה' }])),
      myLessons: vi.fn(() => of([lesson])),
      requestLesson: vi.fn(() => of({})),
      requestChange: vi.fn(() => of({})),
      myChangeRequests: vi.fn(() => of([]))
    };
  });

  it('הגעה עם ?request=1 פותחת את טופס הבקשה, אחרי שהילדים נטענו', () => {
    const component = create({ request: '1' });
    expect(component['showRequestDialog']()).toBe(true);
  });

  it('בקשת שיעור חדש נשלחת עם הסיבה, וסיבה ריקה כ-null', () => {
    const component = create();
    component.openRequestDialog();
    component['requestForm'].patchValue({ studentId: 'kid1', startTime: at(12, 16), endTime: at(12, 17) });

    component.submitRequest();

    expect(portal.requestLesson).toHaveBeenCalledWith({
      studentId: 'kid1', startTime: at(12, 16).toISOString(), endTime: at(12, 17).toISOString(), reason: null
    });
  });

  it('בקשת ביטול נשלחת בלי מועד מוצע', () => {
    const component = create();
    component.openCancelDialog(lesson);
    component['changeForm'].patchValue({ reason: 'מחלה' });

    component.submitChangeRequest();

    expect(portal.requestChange).toHaveBeenCalledWith('l1', {
      type: ChangeRequestType.Cancel, proposedStartTime: null, proposedEndTime: null, reason: 'מחלה'
    });
  });

  it('בקשת שינוי מועד בלי מועד חדש — לא נשלחת', () => {
    const component = create();
    component.openRescheduleDialog(lesson);

    component.submitChangeRequest();

    expect(portal.requestChange).not.toHaveBeenCalled();
    expect(component['changeForm'].controls.proposedStartTime.hasError('required')).toBe(true);
  });

  it('בקשת שינוי מועד שבה הסיום לפני ההתחלה — לא נשלחת', () => {
    const component = create();
    component.openRescheduleDialog(lesson);
    component['changeForm'].patchValue({ proposedStartTime: at(11, 17), proposedEndTime: at(11, 16) });

    component.submitChangeRequest();

    expect(portal.requestChange).not.toHaveBeenCalled();
    expect(component['changeForm'].controls.proposedEndTime.hasError('dateRange')).toBe(true);
  });

  it('בקשת שינוי מועד תקינה נשלחת עם המועד החדש', () => {
    const component = create();
    component.openRescheduleDialog(lesson);
    component['changeForm'].patchValue({ proposedStartTime: at(11, 16), proposedEndTime: at(11, 17) });

    component.submitChangeRequest();

    expect(portal.requestChange).toHaveBeenCalledWith('l1', expect.objectContaining({
      type: ChangeRequestType.Reschedule, proposedStartTime: at(11, 16).toISOString()
    }));
  });

  it('מציגה בקשות ממתינות ובקשות שהשיעור שלהן עוד לפנינו — לא היסטוריה', () => {
    const request = (id: string, status: ChangeRequestStatus, daysFromNow: number) =>
      ({ id, status, lessonStartTime: new Date(Date.now() + daysFromNow * 86_400_000).toISOString() }) as ChangeRequest;
    portal.myChangeRequests = vi.fn(() => of([
      request('pending-past', ChangeRequestStatus.Pending, -3),
      request('approved-future', ChangeRequestStatus.Approved, 3),
      request('rejected-past', ChangeRequestStatus.Rejected, -3)
    ]));
    const component = create();

    expect(component['relevantChangeRequests']().map(r => r.id)).toEqual(['pending-past', 'approved-future']);
  });

  it('אחרי אישור שינוי מועד לא מוצג "מועד מוצע" — השיעור כבר זז אליו', () => {
    const component = create();
    const reschedule = { type: ChangeRequestType.Reschedule, proposedStartTime: at(11, 16).toISOString() } as ChangeRequest;

    expect(component['showsProposedTime']({ ...reschedule, status: ChangeRequestStatus.Pending })).toBe(true);
    expect(component['showsProposedTime']({ ...reschedule, status: ChangeRequestStatus.Approved })).toBe(false);
  });
});
