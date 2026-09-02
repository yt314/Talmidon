import { Component, computed, input, output } from '@angular/core';
import { CalendarOptions, EventClickInfo, EventDropInfo, EventInput, EventResizeDoneInfo } from 'fullcalendar';
import dayGridPlugin from 'fullcalendar/daygrid';
import interactionPlugin from 'fullcalendar/interaction';
import heLocale from 'fullcalendar/locales/he';
import classicTheme from 'fullcalendar/themes/classic';
import timeGridPlugin from 'fullcalendar/timegrid';
import { FullCalendarModule } from '@fullcalendar/angular';
import { CalendarEventDrop, CalendarEventExtendedProps, CalendarSlotSelection } from './lesson-calendar.model';

/**
 * כותרת עמודה ליום: אות היום בשורה עליונה שקטה, ומספר התאריך מתחתיה. ביום הנוכחי
 * המספר מקבל עיגול מלא בצבע המותג — הסימון המקובל ביומנים, וקריא יותר מהדגשת
 * הטקסט בלבד.
 */
function buildDayHeader(date: Date, isToday: boolean): HTMLElement {
  const wrapper = document.createElement('div');
  wrapper.className = 'cal-dayhead';

  const dow = document.createElement('span');
  dow.className = 'cal-dayhead-dow';
  dow.textContent = new Intl.DateTimeFormat('he-IL', { weekday: 'narrow' }).format(date);

  const day = document.createElement('span');
  day.className = isToday ? 'cal-dayhead-num cal-dayhead-today' : 'cal-dayhead-num';
  day.textContent = String(date.getDate());

  wrapper.append(dow, day);
  return wrapper;
}

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
    // גובה חסום עם גלילה פנימית, ולא 'auto': כך היומן לא משתלט על העמוד בשעות
    // הריקות, ו-scrollTime באמת עושה משהו. כל השעות נשארות נגישות בגלילה, ולכן
    // אין סכנה ששיעור מוקדם או מאוחר "ייעלם" (מה שהיה קורה בקיצור טווח השעות).
    height: '68vh',
    firstDay: 0,
    // הדגשת היום הנוכחי — דרך המחלקות האלה ולא בסלקטור CSS, כי v7 מגבב את שמות
    // המחלקות הפנימיות שלו ואין וו יציב לתא של היום
    dayLaneClass: info => (info.isToday ? 'cal-today-lane' : ''),
    dayHeaderClass: info => (info.isToday ? 'cal-today-header' : ''),
    // אותו סיפור בכפתורי הסרגל: אין ‎.fc-button‎ ואין ‎.fc-button-group‎ לתפוס,
    // ולכן המחלקות מוזרקות מכאן ומעוצבות ב-styles.scss
    buttonGroupClass: 'cal-btn-group',
    buttonClass: info =>
      [
        'cal-btn',
        info.buttonGroup ? 'cal-btn-in-group' : 'cal-btn-solo',
        info.isSelected ? 'cal-btn-selected' : '',
        info.isIconOnly ? 'cal-btn-icon' : ''
      ]
        .filter(Boolean)
        .join(' '),
    slotMinTime: '07:00:00',
    slotMaxTime: '22:00:00',
    slotDuration: '00:30:00',
    // תווית לכל שעה עגולה בלבד — חצאי שעה נשארים כקווי רשת בלי מספר, כדי שציר
    // הזמן לא יהיה עמוס במספרים שאיש לא קורא.
    // ‎slotHeader*‎ ולא ‎slotLabel*‎: אלה שמות v6, ו-v7 מתעלם מהם (עם אזהרה בקונסול).
    slotHeaderInterval: '01:00:00',
    slotHeaderFormat: { hour: '2-digit', minute: '2-digit', hour12: false },
    // גובה מינימלי לחצי שעה — נותן לרשת אוויר במקום שורות דחוסות
    slotMinHeight: 26,
    eventTimeFormat: { hour: '2-digit', minute: '2-digit', hour12: false },
    // שורת "כל היום" תמיד ריקה כאן — לשיעור יש תמיד שעה — והיא רק גזלה גובה
    allDaySlot: false,
    // כותרת עמודה בשתי שורות — אות היום מעל מספר התאריך, במקום "יום ג׳ ה-1"
    // בשורה אחת. ‎dayHeaderFormat‎ לבדו לא מספיק כי הוא מייצר מחרוזת אחת ואי אפשר
    // לעצב את שני החלקים בנפרד.
    dayHeaderContent: info => ({ domNodes: [buildDayHeader(info.date, info.isToday)] }),
    nowIndicator: true,
    // נפתח על שעות הפעילות במקום על 07:00, שבדרך כלל ריק
    scrollTime: '08:00:00',
    expandRows: true,
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
