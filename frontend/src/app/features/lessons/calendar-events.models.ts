/** אירוע ביומן שאינו שיעור — פגישה, חופשה, יום חסום. */
export interface CalendarEventItem {
  id: string;
  title: string;
  startTime: string;
  /** באירוע של יום שלם — חצות של היום שאחרי האחרון (סוף בלעדי). */
  endTime: string;
  isAllDay: boolean;
  notes: string | null;
}

export interface SaveCalendarEventRequest {
  title: string;
  startTime: string;
  endTime: string;
  isAllDay: boolean;
  notes: string | null;
}
