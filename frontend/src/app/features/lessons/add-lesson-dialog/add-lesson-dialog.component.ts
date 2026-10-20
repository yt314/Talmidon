import { formatDate } from '@angular/common';
import { Component, LOCALE_ID, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ConfirmationService, MessageService, PrimeTemplate } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { SelectButtonModule } from 'primeng/selectbutton';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { fieldError, isInvalid } from '../../../core/forms/validation-messages';
import { endAfterStartValidator } from '../../../core/forms/validators';
import { CalendarSlotSelection } from '../../../shared/calendar/lesson-calendar.model';
import { StudentListItem } from '../../students/students.models';
import { AvailabilityWindow } from '../../teacher/profile/profile.models';
import { combineDateTime, confirmAnyway, preSaveWarning } from '../lesson-scheduling.util';
import { Lesson, LessonSeriesEndCondition } from '../lessons.models';
import { LessonsService } from '../lessons.service';

interface LessonFormValue {
  studentId: string;
  date: Date | null;
  startTime: Date | null;
  endTime: Date | null;
  recurring: boolean;
  endCondition: LessonSeriesEndCondition;
  occurrenceCount: number;
  endDate: Date | null;
}

/** הוספת שיעור — בודד, או סדרה שבועית. מזהיר לפני קביעה על שיעור קיים או מחוץ לשעות הזמינות. */
@Component({
  selector: 'app-add-lesson-dialog',
  imports: [ReactiveFormsModule, PrimeTemplate, ButtonModule, CheckboxModule, DatePickerModule, DialogModule, InputNumberModule, SelectModule, SelectButtonModule],
  templateUrl: './add-lesson-dialog.component.html'
})
export class AddLessonDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly lessonsService = inject(LessonsService);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly locale = inject(LOCALE_ID);

  readonly students = input<StudentListItem[]>([]);
  /** השיעורים שביומן — לאזהרת כפל-הזמנה. */
  readonly lessons = input<Lesson[]>([]);
  readonly availability = input<AvailabilityWindow[]>([]);
  /** משך ברירת המחדל של המורה, כשלתלמיד אין משך משלו. */
  readonly defaultDurationMinutes = input(60);
  readonly saved = output<void>();

  protected readonly visible = signal(false);
  protected readonly saving = signal(false);
  protected readonly fieldError = fieldError;
  protected readonly isInvalid = isInvalid;
  protected readonly LessonSeriesEndCondition = LessonSeriesEndCondition;

  protected readonly endConditionOptions = [
    { label: 'מספר שיעורים', value: LessonSeriesEndCondition.Count },
    { label: 'עד תאריך', value: LessonSeriesEndCondition.EndDate },
    { label: 'ללא הגבלה', value: LessonSeriesEndCondition.Indefinite }
  ];

  /** מדכא מילוי-אוטומטי של שעת הסיום כשהמקור הוא גרירה ביומן (הטווח כבר נבחר ידנית). */
  private suppressAutoEnd = false;

  protected readonly form = this.fb.nonNullable.group(
    {
      studentId: ['', [Validators.required]],
      date: this.fb.control<Date | null>(null, Validators.required),
      startTime: this.fb.control<Date | null>(null, Validators.required),
      endTime: this.fb.control<Date | null>(null, Validators.required),
      recurring: [false],
      endCondition: [LessonSeriesEndCondition.Count],
      occurrenceCount: [10, [Validators.min(1)]],
      endDate: this.fb.control<Date | null>(null)
    },
    { validators: endAfterStartValidator('startTime', 'endTime') }
  );

  constructor() {
    // מילוי אוטומטי של שעת הסיום לפי משך ברירת המחדל (של התלמיד או של המורה)
    this.form.controls.studentId.valueChanges.subscribe(() => this.applyDefaultEndTime());
    this.form.controls.startTime.valueChanges.subscribe(() => this.applyDefaultEndTime());
  }

  /** פתיחה ריקה, או עם תלמיד שכבר נבחר (כשמגיעים מכרטיס התלמיד). */
  open(studentId?: string): void {
    this.form.reset(defaultFormValue());
    if (studentId) this.form.controls.studentId.setValue(studentId);
    this.visible.set(true);
  }

  /** פתיחה מטווח שנבחר בגרירה ביומן — הטווח נשמר כמו שנבחר. */
  openForSlot(range: CalendarSlotSelection): void {
    this.suppressAutoEnd = true;
    this.form.reset({ ...defaultFormValue(), date: range.start, startTime: range.start, endTime: range.end });
    this.suppressAutoEnd = false;
    this.visible.set(true);
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue();
    if (raw.recurring) {
      this.saveSeries(raw);
      return;
    }

    const start = combineDateTime(raw.date!, raw.startTime!);
    const end = combineDateTime(raw.date!, raw.endTime!);
    const warning = preSaveWarning(this.lessons(), this.availability(), start, end, this.locale);
    if (warning) confirmAnyway(this.confirmationService, warning, () => this.create(raw.studentId, start, end));
    else this.create(raw.studentId, start, end);
  }

  /** קובע שעת סיום = שעת התחלה + משך ברירת המחדל (אלא אם הטווח נבחר בגרירה ביומן). */
  private applyDefaultEndTime(): void {
    if (this.suppressAutoEnd) return;
    const start = this.form.controls.startTime.value;
    if (!start) return;
    const end = new Date(start);
    end.setMinutes(end.getMinutes() + this.durationFor(this.form.controls.studentId.value));
    this.form.controls.endTime.setValue(end, { emitEvent: false });
  }

  /** ערך התלמיד גובר, ואם אין — ברירת המחדל של המורה. */
  private durationFor(studentId: string): number {
    const student = this.students().find(s => s.id === studentId);
    return student?.defaultDurationMinutes ?? this.defaultDurationMinutes();
  }

  private create(studentId: string, start: Date, end: Date): void {
    this.saving.set(true);
    this.lessonsService
      .create({ studentId, startTime: start.toISOString(), endTime: end.toISOString() })
      .subscribe({
        next: () => this.done('השיעור נוסף בהצלחה'),
        error: err => this.failed(err, 'הוספת השיעור נכשלה.')
      });
  }

  private saveSeries(raw: LessonFormValue): void {
    const missing = missingSeriesEnd(raw);
    if (missing) {
      this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: missing });
      return;
    }

    const isCount = raw.endCondition === LessonSeriesEndCondition.Count;
    const isEndDate = raw.endCondition === LessonSeriesEndCondition.EndDate;
    this.saving.set(true);
    this.lessonsService
      .createSeries({
        studentId: raw.studentId,
        firstStartTime: combineDateTime(raw.date!, raw.startTime!).toISOString(),
        firstEndTime: combineDateTime(raw.date!, raw.endTime!).toISOString(),
        endCondition: raw.endCondition,
        occurrenceCount: isCount ? raw.occurrenceCount : null,
        endDate: isEndDate && raw.endDate ? formatDate(raw.endDate, 'yyyy-MM-dd', this.locale) : null
      })
      .subscribe({
        next: result => this.done(`נוצרו ${result.occurrencesCreated} שיעורים חוזרים`),
        error: err => this.failed(err, 'יצירת הסדרה נכשלה.')
      });
  }

  private done(summary: string): void {
    this.saving.set(false);
    this.visible.set(false);
    this.messageService.add({ severity: 'success', summary });
    this.saved.emit();
  }

  private failed(err: unknown, fallback: string): void {
    this.saving.set(false);
    this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, fallback) });
  }
}

function defaultFormValue(): LessonFormValue {
  return {
    studentId: '',
    date: null,
    startTime: null,
    endTime: null,
    recurring: false,
    endCondition: LessonSeriesEndCondition.Count,
    occurrenceCount: 10,
    endDate: null
  };
}

/** מה חסר כדי לדעת מתי הסדרה נגמרת, או null. */
function missingSeriesEnd(raw: LessonFormValue): string | null {
  if (raw.endCondition === LessonSeriesEndCondition.Count && !raw.occurrenceCount) return 'יש להזין מספר שיעורים.';
  if (raw.endCondition === LessonSeriesEndCondition.EndDate && !raw.endDate) return 'יש לבחור תאריך סיום.';
  return null;
}
