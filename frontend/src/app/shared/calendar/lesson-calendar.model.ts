export type CalendarEventKind = 'lesson' | 'request' | 'change-reschedule' | 'change-cancel';

export interface CalendarEventExtendedProps {
  kind: CalendarEventKind;
  refId: string;
}

export interface CalendarSlotSelection {
  start: Date;
  end: Date;
}

export interface CalendarEventDrop {
  refId: string;
  start: Date;
  end: Date;
  /** מחזירה את האירוע חזותית למקומו המקורי — לקרוא לה אם המשתמשת מבטלת באישור, או אם השמירה בשרת נכשלה. */
  revert: () => void;
}
