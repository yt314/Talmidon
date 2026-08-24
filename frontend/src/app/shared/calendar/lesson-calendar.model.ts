export type CalendarEventKind = 'lesson' | 'request' | 'change-reschedule' | 'change-cancel';

export interface CalendarEventExtendedProps {
  kind: CalendarEventKind;
  refId: string;
}

export interface CalendarSlotSelection {
  start: Date;
  end: Date;
}
