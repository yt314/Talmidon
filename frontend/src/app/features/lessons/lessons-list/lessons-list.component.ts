import { DatePipe, formatDate } from '@angular/common';
import { Component, LOCALE_ID, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, FormsModule, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectButtonModule } from 'primeng/selectbutton';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { fieldError, isInvalid } from '../../../core/forms/validation-messages';
import { endAfterStartValidator } from '../../../core/forms/validators';
import { CalendarEventDrop, CalendarEventExtendedProps, CalendarSlotSelection } from '../../../shared/calendar/lesson-calendar.model';
import { LessonCalendarComponent } from '../../../shared/calendar/lesson-calendar.component';
import { StudentListItem } from '../../students/students.models';
import { StudentsService } from '../../students/students.service';
import { buildTeacherCalendarEvents } from '../lesson-calendar.util';
import {
  LESSON_STATUS_LABELS,
  LESSON_STATUS_SEVERITY,
  ChangeRequest,
  ChangeRequestStatus,
  ChangeRequestType,
  Lesson,
  LessonSeriesEndCondition,
  LessonStatus
} from '../lessons.models';
import { LessonsService } from '../lessons.service';

@Component({
  selector: 'app-lessons-list',
  imports: [
    ReactiveFormsModule,
    FormsModule,
    DatePipe,
    ButtonModule,
    CheckboxModule,
    DatePickerModule,
    DialogModule,
    InputNumberModule,
    SelectButtonModule,
    SelectModule,
    TagModule,
    TextareaModule,
    LessonCalendarComponent
  ],
  templateUrl: './lessons-list.component.html'
})
export class LessonsListComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly lessonsService = inject(LessonsService);
  private readonly studentsService = inject(StudentsService);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly locale = inject(LOCALE_ID);

  protected readonly LessonStatus = LessonStatus;
  protected readonly ChangeRequestType = ChangeRequestType;
  protected readonly ChangeRequestStatus = ChangeRequestStatus;
  protected readonly LessonSeriesEndCondition = LessonSeriesEndCondition;
  protected readonly statusLabel = (status: LessonStatus): string => LESSON_STATUS_LABELS[status];
  protected readonly statusSeverity = (status: LessonStatus) => LESSON_STATUS_SEVERITY[status];

  protected readonly endConditionOptions = [
    { label: 'מספר שיעורים', value: LessonSeriesEndCondition.Count },
    { label: 'עד תאריך', value: LessonSeriesEndCondition.EndDate },
    { label: 'ללא הגבלה', value: LessonSeriesEndCondition.Indefinite }
  ];

  protected readonly lessons = signal<Lesson[]>([]);
  protected readonly loading = signal(true);
  protected readonly students = signal<StudentListItem[]>([]);

  protected readonly changeRequests = signal<ChangeRequest[]>([]);
  protected readonly changeRequestsLoading = signal(true);

  protected readonly calendarEvents = computed(() => buildTeacherCalendarEvents(this.lessons(), this.changeRequests()));

  protected readonly showLessonDialog = signal(false);
  protected readonly savingLesson = signal(false);

  protected readonly showEditTimeDialog = signal(false);
  protected readonly editingLessonId = signal<string | null>(null);
  protected readonly savingTime = signal(false);

  protected readonly showCompleteDialog = signal(false);
  protected readonly completingLessonId = signal<string | null>(null);
  protected readonly savingComplete = signal(false);

  protected readonly showLessonDetailDialog = signal(false);
  protected readonly selectedLesson = signal<Lesson | null>(null);

  protected readonly showChangeRequestDialog = signal(false);
  protected readonly selectedChangeRequest = signal<ChangeRequest | null>(null);

  protected readonly showCancelSeriesDialog = signal(false);
  protected readonly cancellingSeriesId = signal<string | null>(null);
  protected readonly cancelSeriesDeleteFuture = signal(false);
  protected readonly cancellingSeries = signal(false);

  protected readonly busyRequestId = signal<string | null>(null);
  protected readonly fieldError = fieldError;
  protected readonly isInvalid = isInvalid;

  protected readonly lessonForm = this.fb.nonNullable.group(
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

  protected readonly timeForm = this.fb.nonNullable.group(
    {
      date: this.fb.control<Date | null>(null, Validators.required),
      startTime: this.fb.control<Date | null>(null, Validators.required),
      endTime: this.fb.control<Date | null>(null, Validators.required)
    },
    { validators: endAfterStartValidator('startTime', 'endTime') }
  );

  protected readonly completeForm = this.fb.nonNullable.group(
    {
      completed: [true],
      paymentRequired: [false],
      amount: [0, [Validators.min(0)]],
      homework: ['', [Validators.maxLength(2000)]],
      noteContent: ['', [Validators.maxLength(4000)]],
      noteVisibleToStudent: [false],
      noteVisibleToParent: [false]
    },
    { validators: amountRequiredWhenChargingValidator }
  );

  ngOnInit(): void {
    this.loadLessons();
    this.loadChangeRequests();
    this.studentsService.list().subscribe(students => this.students.set(students));
  }

  openAddLessonDialog(): void {
    this.lessonForm.reset(defaultLessonFormValue());
    this.showLessonDialog.set(true);
  }

  onSlotSelected(range: CalendarSlotSelection): void {
    this.lessonForm.reset({ ...defaultLessonFormValue(), date: range.start, startTime: range.start, endTime: range.end });
    this.showLessonDialog.set(true);
  }

  /** גרירת שיעור ללוח (רק מתוזמן ניתן לגרירה — נאכף ב-lesson-calendar.util). מבקש אישור לפני שמירה בפועל. */
  onEventDropped(drop: CalendarEventDrop): void {
    const lesson = this.lessons().find(l => l.id === drop.refId);
    if (!lesson) {
      drop.revert();
      return;
    }

    const oldTime = formatDate(lesson.startTime, 'dd/MM/yyyy HH:mm', this.locale);
    const newTime = formatDate(drop.start, 'dd/MM/yyyy HH:mm', this.locale);

    this.confirmationService.confirm({
      header: 'אישור שינוי מועד',
      message: `להעביר את השיעור של ${lesson.studentName} מ-${oldTime} ל-${newTime}?`,
      icon: 'pi pi-calendar',
      acceptLabel: 'עדכן',
      rejectLabel: 'ביטול',
      accept: () => {
        this.lessonsService.update(lesson.id, { startTime: drop.start.toISOString(), endTime: drop.end.toISOString() }).subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'המועד עודכן' });
            this.loadLessons();
          },
          error: err => {
            drop.revert();
            this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'עדכון המועד נכשל.') });
          }
        });
      },
      reject: () => drop.revert()
    });
  }

  /** גרירת קצה השיעור לשינוי משך (רק שיעור "מתוזמן" ניתן לכך — נאכף ב-lesson-calendar.util). מבקש אישור לפני שמירה בפועל. */
  onEventResized(resize: CalendarEventDrop): void {
    const lesson = this.lessons().find(l => l.id === resize.refId);
    if (!lesson) {
      resize.revert();
      return;
    }

    const oldEnd = formatDate(lesson.endTime, 'HH:mm', this.locale);
    const newEnd = formatDate(resize.end, 'HH:mm', this.locale);

    this.confirmationService.confirm({
      header: 'אישור שינוי משך',
      message: `לשנות את משך השיעור של ${lesson.studentName} כך שיסתיים ב-${newEnd} (במקום ${oldEnd})?`,
      icon: 'pi pi-clock',
      acceptLabel: 'עדכן',
      rejectLabel: 'ביטול',
      accept: () => {
        this.lessonsService.update(lesson.id, { startTime: resize.start.toISOString(), endTime: resize.end.toISOString() }).subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'המשך עודכן' });
            this.loadLessons();
          },
          error: err => {
            resize.revert();
            this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'עדכון המשך נכשל.') });
          }
        });
      },
      reject: () => resize.revert()
    });
  }

  onEventClicked(props: CalendarEventExtendedProps): void {
    switch (props.kind) {
      case 'lesson':
      case 'request': {
        const lesson = this.lessons().find(l => l.id === props.refId);
        if (lesson) {
          this.selectedLesson.set(lesson);
          this.showLessonDetailDialog.set(true);
        }
        break;
      }
      case 'change-reschedule':
      case 'change-cancel': {
        const request = this.changeRequests().find(r => r.id === props.refId);
        if (request) {
          this.selectedChangeRequest.set(request);
          this.showChangeRequestDialog.set(true);
        }
        break;
      }
    }
  }

  lessonDetailApprove(): void {
    const lesson = this.selectedLesson();
    if (!lesson) return;
    this.showLessonDetailDialog.set(false);
    this.approveRequest(lesson);
  }

  lessonDetailDecline(): void {
    const lesson = this.selectedLesson();
    if (!lesson) return;
    this.showLessonDetailDialog.set(false);
    this.declineRequest(lesson);
  }

  lessonDetailEditTime(): void {
    const lesson = this.selectedLesson();
    if (!lesson) return;
    this.showLessonDetailDialog.set(false);
    this.openEditTimeDialog(lesson);
  }

  lessonDetailComplete(): void {
    const lesson = this.selectedLesson();
    if (!lesson) return;
    this.showLessonDetailDialog.set(false);
    this.openCompleteDialog(lesson);
  }

  lessonDetailDelete(): void {
    const lesson = this.selectedLesson();
    if (!lesson) return;
    this.showLessonDetailDialog.set(false);
    this.deleteLesson(lesson);
  }

  openCancelSeriesDialog(): void {
    const lesson = this.selectedLesson();
    if (!lesson?.seriesId) return;
    this.showLessonDetailDialog.set(false);
    this.cancellingSeriesId.set(lesson.seriesId);
    this.cancelSeriesDeleteFuture.set(false);
    this.showCancelSeriesDialog.set(true);
  }

  confirmCancelSeries(): void {
    const seriesId = this.cancellingSeriesId();
    if (!seriesId) return;
    this.cancellingSeries.set(true);
    this.lessonsService.cancelSeries(seriesId, this.cancelSeriesDeleteFuture()).subscribe({
      next: () => {
        this.cancellingSeries.set(false);
        this.showCancelSeriesDialog.set(false);
        this.messageService.add({ severity: 'success', summary: 'הסדרה בוטלה' });
        this.loadLessons();
      },
      error: err => {
        this.cancellingSeries.set(false);
        this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'ביטול הסדרה נכשל.') });
      }
    });
  }

  changeRequestDetailApprove(): void {
    const request = this.selectedChangeRequest();
    if (!request) return;
    this.showChangeRequestDialog.set(false);
    this.approveChangeRequest(request);
  }

  changeRequestDetailReject(): void {
    const request = this.selectedChangeRequest();
    if (!request) return;
    this.showChangeRequestDialog.set(false);
    this.rejectChangeRequest(request);
  }

  saveLesson(): void {
    if (this.lessonForm.invalid) {
      this.lessonForm.markAllAsTouched();
      return;
    }
    const raw = this.lessonForm.getRawValue();

    if (raw.recurring) {
      this.saveRecurringLesson(raw);
      return;
    }

    const start = combineDateTime(raw.date!, raw.startTime!);
    const end = combineDateTime(raw.date!, raw.endTime!);

    this.savingLesson.set(true);
    this.lessonsService
      .create({ studentId: raw.studentId, startTime: start.toISOString(), endTime: end.toISOString() })
      .subscribe({
        next: () => {
          this.savingLesson.set(false);
          this.showLessonDialog.set(false);
          this.messageService.add({ severity: 'success', summary: 'השיעור נוסף בהצלחה' });
          this.loadLessons();
        },
        error: err => {
          this.savingLesson.set(false);
          this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'הוספת השיעור נכשלה.') });
        }
      });
  }

  private saveRecurringLesson(raw: ReturnType<typeof this.lessonForm.getRawValue>): void {
    if (raw.endCondition === LessonSeriesEndCondition.Count && !raw.occurrenceCount) {
      this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: 'יש להזין מספר שיעורים.' });
      return;
    }
    if (raw.endCondition === LessonSeriesEndCondition.EndDate && !raw.endDate) {
      this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: 'יש לבחור תאריך סיום.' });
      return;
    }

    const start = combineDateTime(raw.date!, raw.startTime!);
    const end = combineDateTime(raw.date!, raw.endTime!);

    this.savingLesson.set(true);
    this.lessonsService
      .createSeries({
        studentId: raw.studentId,
        firstStartTime: start.toISOString(),
        firstEndTime: end.toISOString(),
        endCondition: raw.endCondition,
        occurrenceCount: raw.endCondition === LessonSeriesEndCondition.Count ? raw.occurrenceCount : null,
        endDate: raw.endCondition === LessonSeriesEndCondition.EndDate && raw.endDate ? formatDate(raw.endDate, 'yyyy-MM-dd', this.locale) : null
      })
      .subscribe({
        next: result => {
          this.savingLesson.set(false);
          this.showLessonDialog.set(false);
          this.messageService.add({ severity: 'success', summary: `נוצרו ${result.occurrencesCreated} שיעורים חוזרים` });
          this.loadLessons();
        },
        error: err => {
          this.savingLesson.set(false);
          this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'יצירת הסדרה נכשלה.') });
        }
      });
  }

  openEditTimeDialog(lesson: Lesson): void {
    this.editingLessonId.set(lesson.id);
    const start = new Date(lesson.startTime);
    const end = new Date(lesson.endTime);
    this.timeForm.reset({ date: start, startTime: start, endTime: end });
    this.showEditTimeDialog.set(true);
  }

  saveTime(): void {
    if (this.timeForm.invalid) {
      this.timeForm.markAllAsTouched();
      return;
    }
    const id = this.editingLessonId();
    if (!id) return;
    const raw = this.timeForm.getRawValue();
    const start = combineDateTime(raw.date!, raw.startTime!);
    const end = combineDateTime(raw.date!, raw.endTime!);
    this.savingTime.set(true);
    this.lessonsService.update(id, { startTime: start.toISOString(), endTime: end.toISOString() }).subscribe({
      next: () => {
        this.savingTime.set(false);
        this.showEditTimeDialog.set(false);
        this.messageService.add({ severity: 'success', summary: 'המועד עודכן' });
        this.loadLessons();
      },
      error: err => {
        this.savingTime.set(false);
        this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'עדכון המועד נכשל.') });
      }
    });
  }

  deleteLesson(lesson: Lesson): void {
    this.lessonsService.delete(lesson.id).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'השיעור נמחק' });
        this.loadLessons();
      },
      error: err => this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'המחיקה נכשלה.') })
    });
  }

  openCompleteDialog(lesson: Lesson): void {
    this.completingLessonId.set(lesson.id);
    this.completeForm.reset({
      completed: true,
      paymentRequired: false,
      amount: 0,
      homework: '',
      noteContent: '',
      noteVisibleToStudent: false,
      noteVisibleToParent: false
    });
    this.showCompleteDialog.set(true);
  }

  saveComplete(): void {
    const id = this.completingLessonId();
    if (!id) return;
    if (this.completeForm.invalid) {
      this.completeForm.markAllAsTouched();
      return;
    }
    const raw = this.completeForm.getRawValue();
    this.savingComplete.set(true);
    this.lessonsService
      .complete(id, {
        completed: raw.completed,
        paymentRequired: raw.paymentRequired,
        amount: raw.amount,
        homework: raw.homework || null,
        noteContent: raw.noteContent || null,
        noteVisibleToStudent: raw.noteVisibleToStudent,
        noteVisibleToParent: raw.noteVisibleToParent
      })
      .subscribe({
        next: () => {
          this.savingComplete.set(false);
          this.showCompleteDialog.set(false);
          this.messageService.add({ severity: 'success', summary: 'השיעור עודכן' });
          this.loadLessons();
        },
        error: err => {
          this.savingComplete.set(false);
          this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'העדכון נכשל.') });
        }
      });
  }

  approveRequest(lesson: Lesson): void {
    this.busyRequestId.set(lesson.id);
    this.lessonsService.approveRequest(lesson.id).subscribe({
      next: () => {
        this.busyRequestId.set(null);
        this.messageService.add({ severity: 'success', summary: 'הבקשה אושרה' });
        this.loadLessons();
      },
      error: err => {
        this.busyRequestId.set(null);
        this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'האישור נכשל.') });
      }
    });
  }

  declineRequest(lesson: Lesson): void {
    this.busyRequestId.set(lesson.id);
    this.lessonsService.declineRequest(lesson.id).subscribe({
      next: () => {
        this.busyRequestId.set(null);
        this.messageService.add({ severity: 'success', summary: 'הבקשה נדחתה' });
        this.loadLessons();
      },
      error: err => {
        this.busyRequestId.set(null);
        this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'הדחייה נכשלה.') });
      }
    });
  }

  approveChangeRequest(request: ChangeRequest): void {
    this.busyRequestId.set(request.id);
    this.lessonsService.approveChangeRequest(request.id).subscribe({
      next: () => {
        this.busyRequestId.set(null);
        this.messageService.add({ severity: 'success', summary: 'הבקשה אושרה' });
        this.loadLessons();
        this.loadChangeRequests();
      },
      error: err => {
        this.busyRequestId.set(null);
        this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'האישור נכשל.') });
      }
    });
  }

  rejectChangeRequest(request: ChangeRequest): void {
    this.busyRequestId.set(request.id);
    this.lessonsService.rejectChangeRequest(request.id).subscribe({
      next: () => {
        this.busyRequestId.set(null);
        this.messageService.add({ severity: 'success', summary: 'הבקשה נדחתה' });
        this.loadChangeRequests();
      },
      error: err => {
        this.busyRequestId.set(null);
        this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'הדחייה נכשלה.') });
      }
    });
  }

  private loadLessons(): void {
    this.loading.set(true);
    this.lessonsService.list().subscribe({
      next: lessons => {
        this.lessons.set(lessons);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  private loadChangeRequests(): void {
    this.changeRequestsLoading.set(true);
    this.lessonsService.listChangeRequests(ChangeRequestStatus.Pending).subscribe({
      next: requests => {
        this.changeRequests.set(requests);
        this.changeRequestsLoading.set(false);
      },
      error: () => this.changeRequestsLoading.set(false)
    });
  }
}

function defaultLessonFormValue() {
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

/** משלב תאריך (חלק היום/חודש/שנה) עם שעה (חלק השעה/דקה) לאובייקט Date אחד. */
function combineDateTime(date: Date, time: Date): Date {
  const combined = new Date(date);
  combined.setHours(time.getHours(), time.getMinutes(), 0, 0);
  return combined;
}

/** כשמסמנים שיעור כהתקיים+נדרש תשלום, יש להזין סכום גדול מאפס. */
function amountRequiredWhenChargingValidator(group: AbstractControl): ValidationErrors | null {
  const completed = group.get('completed')?.value;
  const paymentRequired = group.get('paymentRequired')?.value;
  const amountControl = group.get('amount');
  if (!amountControl) return null;

  const needsAmount = completed && paymentRequired && (amountControl.value ?? 0) <= 0;
  const currentErrors = amountControl.errors ?? {};
  const hasRequired = !!currentErrors['required'];

  if (needsAmount && !hasRequired) {
    amountControl.setErrors({ ...currentErrors, required: true });
  } else if (!needsAmount && hasRequired) {
    const { required, ...rest } = currentErrors;
    amountControl.setErrors(Object.keys(rest).length > 0 ? rest : null);
  }
  return null;
}
