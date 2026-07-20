import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TabsModule } from 'primeng/tabs';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { TooltipModule } from 'primeng/tooltip';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { fieldError, isInvalid } from '../../../core/forms/validation-messages';
import { endAfterStartValidator } from '../../../core/forms/validators';
import { StudentListItem } from '../../students/students.models';
import { StudentsService } from '../../students/students.service';
import {
  LESSON_STATUS_LABELS,
  ChangeRequest,
  ChangeRequestStatus,
  ChangeRequestType,
  Lesson,
  LessonStatus
} from '../lessons.models';
import { LessonsService } from '../lessons.service';

@Component({
  selector: 'app-lessons-list',
  imports: [
    ReactiveFormsModule,
    DatePipe,
    ButtonModule,
    CheckboxModule,
    DatePickerModule,
    DialogModule,
    InputNumberModule,
    SelectModule,
    TableModule,
    TabsModule,
    TagModule,
    TextareaModule,
    TooltipModule
  ],
  templateUrl: './lessons-list.component.html'
})
export class LessonsListComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly lessonsService = inject(LessonsService);
  private readonly studentsService = inject(StudentsService);
  private readonly messageService = inject(MessageService);

  protected readonly LessonStatus = LessonStatus;
  protected readonly ChangeRequestType = ChangeRequestType;
  protected readonly ChangeRequestStatus = ChangeRequestStatus;
  protected readonly statusLabel = (status: LessonStatus): string => LESSON_STATUS_LABELS[status];

  protected readonly lessons = signal<Lesson[]>([]);
  protected readonly loading = signal(true);
  protected readonly students = signal<StudentListItem[]>([]);

  protected readonly changeRequests = signal<ChangeRequest[]>([]);
  protected readonly changeRequestsLoading = signal(true);

  protected readonly showLessonDialog = signal(false);
  protected readonly savingLesson = signal(false);

  protected readonly showEditTimeDialog = signal(false);
  protected readonly editingLessonId = signal<string | null>(null);
  protected readonly savingTime = signal(false);

  protected readonly showCompleteDialog = signal(false);
  protected readonly completingLessonId = signal<string | null>(null);
  protected readonly savingComplete = signal(false);

  protected readonly busyRequestId = signal<string | null>(null);
  protected readonly fieldError = fieldError;
  protected readonly isInvalid = isInvalid;

  protected readonly lessonForm = this.fb.nonNullable.group(
    {
      studentId: ['', [Validators.required]],
      startTime: this.fb.control<Date | null>(null, Validators.required),
      endTime: this.fb.control<Date | null>(null, Validators.required)
    },
    { validators: endAfterStartValidator('startTime', 'endTime') }
  );

  protected readonly timeForm = this.fb.nonNullable.group(
    {
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
    this.lessonForm.reset({ studentId: '', startTime: null, endTime: null });
    this.showLessonDialog.set(true);
  }

  saveLesson(): void {
    if (this.lessonForm.invalid) {
      this.lessonForm.markAllAsTouched();
      return;
    }
    const raw = this.lessonForm.getRawValue();
    this.savingLesson.set(true);
    this.lessonsService
      .create({ studentId: raw.studentId, startTime: raw.startTime!.toISOString(), endTime: raw.endTime!.toISOString() })
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

  openEditTimeDialog(lesson: Lesson): void {
    this.editingLessonId.set(lesson.id);
    this.timeForm.reset({ startTime: new Date(lesson.startTime), endTime: new Date(lesson.endTime) });
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
    this.savingTime.set(true);
    this.lessonsService.update(id, { startTime: raw.startTime!.toISOString(), endTime: raw.endTime!.toISOString() }).subscribe({
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

  protected statusSeverity(status: LessonStatus): 'success' | 'info' | 'warn' | 'danger' | 'secondary' {
    switch (status) {
      case LessonStatus.Completed:
        return 'success';
      case LessonStatus.Scheduled:
        return 'info';
      case LessonStatus.Requested:
        return 'warn';
      case LessonStatus.Cancelled:
      case LessonStatus.Declined:
        return 'danger';
      default:
        return 'secondary';
    }
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
