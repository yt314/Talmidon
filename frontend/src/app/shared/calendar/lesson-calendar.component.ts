import { Component, computed, input, output } from '@angular/core';
import { CalendarOptions, EventClickInfo, EventDropInfo, EventInput, EventResizeDoneInfo } from 'fullcalendar';
import dayGridPlugin from 'fullcalendar/daygrid';
import interactionPlugin from 'fullcalendar/interaction';
import heLocale from 'fullcalendar/locales/he';
import classicTheme from 'fullcalendar/themes/classic';
import timeGridPlugin from 'fullcalendar/timegrid';
import { FullCalendarModule } from '@fullcalendar/angular';
import { CalendarEventDrop, CalendarEventExtendedProps, CalendarSlotSelection } from './lesson-calendar.model';

/** עטיפה משותפת סביב FullCalendar — RTL/עברית, צביעה לפי סטטוס וטיפול בלחיצות. משמשת את יומני המורה/הורה/תלמיד. */
@Component({
  selector: 'app-lesson-calendar',
  imports: [FullCalendarModule],
  template: `<full-calendar [options]="calendarOptions()" />`
})
export class LessonCalendarComponent {
  readonly events = input<EventInput[]>([]);
  readonly selectable = input(false);
  /**
   * מפעילה גרירה/שינוי-משך של אירועים; אילו אירועים בפועל ניתנים לכך נקבע פר-אירוע
   * (startEditable / durationEditable) ב-EventInput עצמו.
   */
  readonly editable = input(false);
  readonly initialView = input<'timeGridWeek' | 'dayGridMonth'>('timeGridWeek');
  /** שעות עבודה להדגשה (FullCalendar businessHours). ריק/undefined = ללא הדגשה. */
  readonly businessHours = input<{ daysOfWeek: number[]; startTime: string; endTime: string }[] | undefined>(undefined);

  readonly eventClicked = output<CalendarEventExtendedProps>();
  readonly slotSelected = output<CalendarSlotSelection>();
  readonly eventDropped = output<CalendarEventDrop>();
  readonly eventResized = output<CalendarEventDrop>();

  protected readonly calendarOptions = computed<CalendarOptions>(() => ({
    plugins: [dayGridPlugin, timeGridPlugin, interactionPlugin, classicTheme],
    locale: heLocale,
    direction: 'rtl',
    initialView: this.initialView(),
    headerToolbar: { start: 'prev,next today', center: 'title', end: 'dayGridMonth,timeGridWeek' },
    height: 'auto',
    firstDay: 0,
    slotMinTime: '07:00:00',
    slotMaxTime: '22:00:00',
    slotDuration: '00:30:00',
    nowIndicator: true,
    dayMaxEvents: true,
    selectable: this.selectable(),
    editable: this.editable(),
    businessHours: this.businessHours() ?? false,
    events: this.events(),
    select: info => {
      if (!info.allDay) {
        this.slotSelected.emit({ start: info.start, end: info.end });
        return;
      }
      const start = new Date(info.start);
      start.setHours(9, 0, 0, 0);
      const end = new Date(start.getTime() + 60 * 60 * 1000);
      this.slotSelected.emit({ start, end });
    },
    eventClick: (info: EventClickInfo) => {
      info.jsEvent.preventDefault();
      this.eventClicked.emit(info.event.extendedProps as CalendarEventExtendedProps);
    },
    eventDrop: (info: EventDropInfo) => {
      const { refId } = info.event.extendedProps as CalendarEventExtendedProps;
      this.eventDropped.emit({
        refId,
        start: info.event.start!,
        end: info.event.end!,
        revert: info.revert
      });
    },
    eventResize: (info: EventResizeDoneInfo) => {
      const { refId } = info.event.extendedProps as CalendarEventExtendedProps;
      this.eventResized.emit({
        refId,
        start: info.event.start!,
        end: info.event.end!,
        revert: info.revert
      });
    }
  }));
}
