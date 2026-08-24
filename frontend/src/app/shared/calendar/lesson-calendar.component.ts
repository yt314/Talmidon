import { Component, computed, input, output } from '@angular/core';
import { CalendarOptions, EventClickInfo, EventInput } from 'fullcalendar';
import dayGridPlugin from 'fullcalendar/daygrid';
import interactionPlugin from 'fullcalendar/interaction';
import heLocale from 'fullcalendar/locales/he';
import classicTheme from 'fullcalendar/themes/classic';
import timeGridPlugin from 'fullcalendar/timegrid';
import { FullCalendarModule } from '@fullcalendar/angular';
import { CalendarEventExtendedProps, CalendarSlotSelection } from './lesson-calendar.model';

/** עטיפה משותפת סביב FullCalendar — RTL/עברית, צביעה לפי סטטוס וטיפול בלחיצות. משמשת את יומני המורה/הורה/תלמיד. */
@Component({
  selector: 'app-lesson-calendar',
  imports: [FullCalendarModule],
  template: `<full-calendar [options]="calendarOptions()" />`
})
export class LessonCalendarComponent {
  readonly events = input<EventInput[]>([]);
  readonly selectable = input(false);
  readonly initialView = input<'timeGridWeek' | 'dayGridMonth'>('timeGridWeek');

  readonly eventClicked = output<CalendarEventExtendedProps>();
  readonly slotSelected = output<CalendarSlotSelection>();

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
    }
  }));
}
