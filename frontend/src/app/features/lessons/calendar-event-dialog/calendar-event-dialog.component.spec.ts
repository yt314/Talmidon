import { TestBed } from '@angular/core/testing';
import { Confirmation, ConfirmationService, MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { CalendarEventsService } from '../calendar-events.service';
import { CalendarEventDialogComponent } from './calendar-event-dialog.component';

const at = (day: number, hour: number, minute = 0) => new Date(2026, 2, day, hour, minute);

describe('CalendarEventDialogComponent', () => {
  let component: CalendarEventDialogComponent;
  let service: { create: ReturnType<typeof vi.fn>; update: ReturnType<typeof vi.fn>; remove: ReturnType<typeof vi.fn> };
  let confirmations: Confirmation[];
  let changed: number;

  beforeEach(() => {
    service = { create: vi.fn(() => of({})), update: vi.fn(() => of({})), remove: vi.fn(() => of(undefined)) };
    confirmations = [];
    TestBed.configureTestingModule({
      imports: [CalendarEventDialogComponent],
      providers: [MessageService, ConfirmationService, { provide: CalendarEventsService, useValue: service }]
    });
    vi.spyOn(TestBed.inject(ConfirmationService), 'confirm').mockImplementation(c => {
      confirmations.push(c);
      return TestBed.inject(ConfirmationService);
    });
    component = TestBed.createComponent(CalendarEventDialogComponent).componentInstance;
    changed = 0;
    component.changed.subscribe(() => changed++);
  });

  const form = () => component['form'];

  it('אירוע חדש נשמר כחדש ומודיע ליומן', () => {
    component.open();
    form().patchValue({ title: 'פגישה', date: at(10, 0), startTime: at(10, 9), endTime: at(10, 10) });

    component['save']();

    expect(service.create).toHaveBeenCalledWith(expect.objectContaining({ title: 'פגישה', startTime: at(10, 9).toISOString() }));
    expect(changed).toBe(1);
  });

  it('אירוע קיים נשמר כעדכון', () => {
    component.open({ id: 'e1', title: 'חג', startTime: at(10, 0).toISOString(), endTime: at(11, 0).toISOString(), isAllDay: true, notes: null });

    component['save']();

    expect(service.update).toHaveBeenCalledWith('e1', expect.objectContaining({ isAllDay: true, endTime: at(11, 0).toISOString() }));
  });

  it('שעת סיום לפני שעת ההתחלה — שגיאת dateRange, ולא נשלח', () => {
    component.open();
    form().patchValue({ title: 'x', date: at(10, 0), startTime: at(10, 10), endTime: at(10, 9) });

    component['save']();

    expect(form().errors?.['dateRange']).toBe(true);
    expect(service.create).not.toHaveBeenCalled();
  });

  it('ביום שלם אין בדיקת סדר שעות', () => {
    component.open();
    form().patchValue({ title: 'x', date: at(10, 0), isAllDay: true, startTime: at(10, 10), endTime: at(10, 9) });
    expect(form().errors).toBeNull();
  });

  it('מחיקה רק אחרי אישור', () => {
    component.open({ id: 'e1', title: 'x', startTime: at(10, 9).toISOString(), endTime: at(10, 10).toISOString(), isAllDay: false, notes: null });

    component['remove']();
    expect(service.remove).not.toHaveBeenCalled();

    confirmations[0].accept!();
    expect(service.remove).toHaveBeenCalledWith('e1');
    expect(changed).toBe(1);
  });
});
