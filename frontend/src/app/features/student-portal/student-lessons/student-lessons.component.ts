
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { EventInput } from 'fullcalendar';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { endAfterStartValidator } from '../../../core/forms/validators';
import { fieldError, isInvalid } from '../../../core/forms/validation-messages';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { PageHeaderComponent } from '../../../shared/ui/page-header.component';
import { CalendarEventExtendedProps } from '../../../shared/calendar/lesson-calendar.model';
import { LessonCalendarComponent } from '../../../shared/calendar/lesson-calendar.component';
import { LESSON_STATUS_CLASS, LESSON_STATUS_LABELS, LESSON_STATUS_SEVERITY, LessonStatus } from '../../lessons/lessons.models';
import { StudentLesson } from '../student-portal.models';
import { StudentPortalService } from '../student-portal.service';
import { IsraelDatePipe } from '../../../core/i18n/israel-date.pipe';

@Component({
  selector: 'app-student-lessons',
  imports: [ ReactiveFormsModule,
    ButtonModule,
    DatePickerModule,
    DialogModule,
    TagModule,
    TextareaModule,
    LessonCalendarComponent,
    PageHeaderComponent, IsraelDatePipe],
  templateUrl: './student-lessons.component.html'
})
export class StudentLessonsComponent implements OnInit {
  private readonly portalService = inject(StudentPortalService);
  private readonly fb = inject(FormBuilder);
  private readonly messageService = inject(MessageService);

  protected readonly fieldError = fieldError;
  protected readonly isInvalid = isInvalid;

  protected readonly statusLabel = (status: LessonStatus): string => LESSON_STATUS_LABELS[status];
  protected readonly statusSeverity = (status: LessonStatus) => LESSON_STATUS_SEVERITY[status];
  protected readonly lessons = signal<StudentLesson[]>([]);
  protected readonly loading = signal(true);

  protected readonly showLessonDetailDialog = signal(false);
  protected readonly selectedLesson = signal<StudentLesson | null>(null);

  protected readonly showRequestDialog = signal(false);
  protected readonly savingRequest = signal(false);

  protected readonly requestForm = this.fb.nonNullable.group(
    {
      startTime: this.fb.control<Date | null>(null, Validators.required),
      endTime: this.fb.control<Date | null>(null, Validators.required),
      reason: ['', [Validators.maxLength(1000)]]
    },
    { validators: endAfterStartValidator('startTime', 'endTime') }
  );

  protected readonly calendarEvents = computed<EventInput[]>(() =>
    this.lessons().map(lesson => {
      const statusClass = LESSON_STATUS_CLASS[lesson.status];
      const extendedProps: CalendarEventExtendedProps = { kind: 'lesson', refId: lesson.id };
      return {
        id: lesson.id,
        title: this.statusLabel(lesson.status),
        start: lesson.startTime,
        end: lesson.endTime,
        className: lesson.status === LessonStatus.Cancelled ? `${statusClass} lesson-cal-muted` : statusClass,
        extendedProps
      };
    })
  );

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.portalService.mySchedule().subscribe({
      next: lessons => {
        this.lessons.set(lessons);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  /**
   * ברירת מחדל לשעה עגולה מחר: מי שמבקש שיעור מתכוון כמעט תמיד לימים הקרובים, וכך
   * נשאר רק לתקן שעה במקום לבחור תאריך מאפס.
   */
  openRequestDialog(): void {
    const start = new Date();
    start.setDate(start.getDate() + 1);
    start.setHours(16, 0, 0, 0);
    const end = new Date(start.getTime() + 60 * 60 * 1000);

    this.requestForm.reset({ startTime: start, endTime: end, reason: '' });
    this.showRequestDialog.set(true);
  }

  submitRequest(): void {
    if (this.requestForm.invalid) {
      this.requestForm.markAllAsTouched();
      return;
    }

    const raw = this.requestForm.getRawValue();
    this.savingRequest.set(true);
    this.portalService
      .requestLesson({
        startTime: raw.startTime!.toISOString(),
        endTime: raw.endTime!.toISOString(),
        reason: raw.reason.trim() || null
      })
      .subscribe({
        next: () => {
          this.savingRequest.set(false);
          this.showRequestDialog.set(false);
          this.messageService.add({ severity: 'success', summary: 'הבקשה נשלחה למורה' });
          this.load();
        },
        error: err => {
          this.savingRequest.set(false);
          this.messageService.add({
            severity: 'error',
            summary: 'שגיאה',
            detail: extractErrorMessage(err, 'שליחת הבקשה נכשלה.')
          });
        }
      });
  }

  onEventClicked(props: CalendarEventExtendedProps): void {
    const lesson = this.lessons().find(l => l.id === props.refId);
    if (!lesson) return;
    this.selectedLesson.set(lesson);
    this.showLessonDetailDialog.set(true);
  }
}
