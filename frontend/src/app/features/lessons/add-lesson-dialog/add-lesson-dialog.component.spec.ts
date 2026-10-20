import { TestBed } from '@angular/core/testing';
import { Confirmation, ConfirmationService, MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { StudentListItem } from '../../students/students.models';
import { Lesson, LessonSeriesEndCondition, LessonStatus } from '../lessons.models';
import { LessonsService } from '../lessons.service';
import { AddLessonDialogComponent } from './add-lesson-dialog.component';

/** תאריך בשעון המקומי — הבדיקות לא תלויות באזור הזמן של המכונה. */
const at = (day: number, hour: number, minute = 0) => new Date(2026, 2, day, hour, minute);

const students = [
  { id: 's1', fullName: 'דנה', defaultDurationMinutes: 45 },
  { id: 's2', fullName: 'יעל', defaultDurationMinutes: null }
] as StudentListItem[];

describe('AddLessonDialogComponent', () => {
  let component: AddLessonDialogComponent;
  let create: ReturnType<typeof vi.fn>;
  let createSeries: ReturnType<typeof vi.fn>;
  let confirmations: Confirmation[];

  beforeEach(() => {
    create = vi.fn(() => of({}));
    createSeries = vi.fn(() => of({ occurrencesCreated: 10 }));
    confirmations = [];
    TestBed.configureTestingModule({
      imports: [AddLessonDialogComponent],
      providers: [MessageService, ConfirmationService, { provide: LessonsService, useValue: { create, createSeries } }]
    });
    vi.spyOn(TestBed.inject(ConfirmationService), 'confirm').mockImplementation(c => {
      confirmations.push(c);
      return TestBed.inject(ConfirmationService);
    });

    const fixture = TestBed.createComponent(AddLessonDialogComponent);
    fixture.componentRef.setInput('students', students);
    fixture.componentRef.setInput('defaultDurationMinutes', 60);
    component = fixture.componentInstance;
  });

  const form = () => component['form'];

  it('ממלא שעת סיום לפי משך ברירת המחדל של התלמיד', () => {
    component.open('s1');
    form().patchValue({ startTime: at(10, 14) });
    expect(form().controls.endTime.value).toEqual(at(10, 14, 45));
  });

  it('תלמיד בלי משך משלו — לפי משך ברירת המחדל של המורה', () => {
    component.open('s2');
    form().patchValue({ startTime: at(10, 14) });
    expect(form().controls.endTime.value).toEqual(at(10, 15));
  });

  it('טווח שנבחר בגרירה ביומן נשמר כמו שנבחר', () => {
    component.openForSlot({ start: at(10, 14), end: at(10, 16) } as never);
    expect(form().controls.endTime.value).toEqual(at(10, 16));
  });

  it('קובע שיעור כשאין התנגשות', () => {
    component.open('s1');
    form().patchValue({ date: at(10, 0), startTime: at(10, 14), endTime: at(10, 15) });

    component['save']();

    expect(create).toHaveBeenCalledWith({ studentId: 's1', startTime: at(10, 14).toISOString(), endTime: at(10, 15).toISOString() });
    expect(confirmations).toHaveLength(0);
  });

  it('מזהיר על שיעור קיים באותה שעה, וקובע רק אחרי אישור', () => {
    const existing = { id: 'x', studentName: 'יעל', status: LessonStatus.Scheduled, startTime: at(10, 14).toISOString(), endTime: at(10, 15).toISOString() } as Lesson;
    const fixture = TestBed.createComponent(AddLessonDialogComponent);
    fixture.componentRef.setInput('lessons', [existing]);
    const dialog = fixture.componentInstance;
    dialog.open('s1');
    dialog['form'].patchValue({ date: at(10, 0), startTime: at(10, 14, 30), endTime: at(10, 15, 30) });

    dialog['save']();

    expect(create).not.toHaveBeenCalled();
    expect(confirmations[0].message).toContain('כבר קיים שיעור עם יעל');
    confirmations[0].accept!();
    expect(create).toHaveBeenCalledTimes(1);
  });

  it('שיעור חוזר נשלח כסדרה עם מספר השיעורים', () => {
    component.open('s1');
    form().patchValue({ date: at(10, 0), startTime: at(10, 14), endTime: at(10, 15), recurring: true, occurrenceCount: 8 });

    component['save']();

    expect(create).not.toHaveBeenCalled();
    expect(createSeries).toHaveBeenCalledWith(expect.objectContaining({
      studentId: 's1', endCondition: LessonSeriesEndCondition.Count, occurrenceCount: 8, endDate: null
    }));
  });

  it('סדרה "עד תאריך" בלי תאריך אינה נשלחת', () => {
    component.open('s1');
    form().patchValue({
      date: at(10, 0), startTime: at(10, 14), endTime: at(10, 15), recurring: true, endCondition: LessonSeriesEndCondition.EndDate
    });

    component['save']();

    expect(createSeries).not.toHaveBeenCalled();
  });
});
