import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { PageHeaderComponent } from '../../../shared/ui/page-header.component';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { fieldError, isInvalid } from '../../../core/forms/validation-messages';
import { endAfterStartValidator } from '../../../core/forms/validators';
import { CalendarEventExtendedProps } from '../../../shared/calendar/lesson-calendar.model';
import { LessonCalendarComponent } from '../../../shared/calendar/lesson-calendar.component';
import { lessonToCalendarEvent } from '../../lessons/lesson-calendar.util';
import { LESSON_STATUS_LABELS, LESSON_STATUS_SEVERITY, ChangeRequestType, Lesson, LessonStatus } from '../../lessons/lessons.models';
import { MyChild } from '../parent-portal.models';
import { ParentPortalService } from '../parent-portal.service';

@Component({
  selector: 'app-parent-lessons',
  imports: [ReactiveFormsModule,
    FormsModule,
    DatePipe,
    ButtonModule,
    DatePickerModule,
    DialogModule,
    InputTextModule,
    SelectModule,
    TagModule,
    LessonCalendarComponent, PageHeaderComponent],
  templateUrl: './parent-lessons.component.html'
})
export class ParentLessonsComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly portalService = inject(ParentPortalService);
  private readonly messageService = inject(MessageService);

  protected readonly LessonStatus = LessonStatus;
  protected readonly ChangeRequestType = ChangeRequestType;
  protected readonly statusLabel = (status: LessonStatus): string => LESSON_STATUS_LABELS[status];
  protected readonly statusSeverity = (status: LessonStatus) => LESSON_STATUS_SEVERITY[status];

  protected readonly children = signal<MyChild[]>([]);
  protected readonly selectedChildId = signal<string | null>(null);
  protected readonly lessons = signal<Lesson[]>([]);
  protected readonly loading = signal(true);

  protected readonly calendarEvents = computed(() => this.lessons().map(lessonToCalendarEvent));

  protected readonly showLessonDetailDialog = signal(false);
  protected readonly selectedLesson = signal<Lesson | null>(null);

  protected readonly showRequestDialog = signal(false);
  protected readonly savingRequest = signal(false);

  protected readonly showChangeDialog = signal(false);
  protected readonly changeRequestType = signal<ChangeRequestType>(ChangeRequestType.Cancel);
  protected readonly changingLessonId = signal<string | null>(null);
  protected readonly savingChange = signal(false);
  protected readonly fieldError = fieldError;
  protected readonly isInvalid = isInvalid;

  protected readonly requestForm = this.fb.nonNullable.group(
    {
      studentId: ['', [Validators.required]],
      startTime: this.fb.control<Date | null>(null, Validators.required),
      endTime: this.fb.control<Date | null>(null, Validators.required),
      reason: ['', [Validators.maxLength(1000)]]
    },
    { validators: endAfterStartValidator('startTime', 'endTime') }
  );

  protected readonly changeForm = this.fb.nonNullable.group({
    proposedStartTime: this.fb.control<Date | null>(null),
    proposedEndTime: this.fb.control<Date | null>(null),
    reason: ['', [Validators.maxLength(1000)]]
  });

  ngOnInit(): void {
    this.portalService.myChildren().subscribe(children => this.children.set(children));
    this.load();
  }

  onChildChange(): void {
    this.load();
  }

  onEventClicked(props: CalendarEventExtendedProps): void {
    const lesson = this.lessons().find(l => l.id === props.refId);
    if (!lesson) return;
    this.selectedLesson.set(lesson);
    this.showLessonDetailDialog.set(true);
  }

  lessonDetailReschedule(): void {
    const lesson = this.selectedLesson();
    if (!lesson) return;
    this.showLessonDetailDialog.set(false);
    this.openRescheduleDialog(lesson);
  }

  lessonDetailCancel(): void {
    const lesson = this.selectedLesson();
    if (!lesson) return;
    this.showLessonDetailDialog.set(false);
    this.openCancelDialog(lesson);
  }

  openRequestDialog(): void {
    this.requestForm.reset({ studentId: this.selectedChildId() ?? '', startTime: null, endTime: null, reason: '' });
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
        studentId: raw.studentId,
        startTime: raw.startTime!.toISOString(),
        endTime: raw.endTime!.toISOString(),
        reason: raw.reason || null
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
          this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'שליחת הבקשה נכשלה.') });
        }
      });
  }

  openCancelDialog(lesson: Lesson): void {
    this.changeRequestType.set(ChangeRequestType.Cancel);
    this.changingLessonId.set(lesson.id);
    this.changeForm.reset({ proposedStartTime: null, proposedEndTime: null, reason: '' });
    this.showChangeDialog.set(true);
  }

  openRescheduleDialog(lesson: Lesson): void {
    this.changeRequestType.set(ChangeRequestType.Reschedule);
    this.changingLessonId.set(lesson.id);
    this.changeForm.reset({ proposedStartTime: null, proposedEndTime: null, reason: '' });
    this.showChangeDialog.set(true);
  }

  submitChangeRequest(): void {
    const lessonId = this.changingLessonId();
    if (!lessonId) return;
    const type = this.changeRequestType();
    const raw = this.changeForm.getRawValue();

    if (this.changeForm.invalid) {
      this.changeForm.markAllAsTouched();
      return;
    }

    if (type === ChangeRequestType.Reschedule) {
      const startControl = this.changeForm.controls.proposedStartTime;
      const endControl = this.changeForm.controls.proposedEndTime;
      startControl.markAsTouched();
      endControl.markAsTouched();
      if (!raw.proposedStartTime) startControl.setErrors({ required: true });
      if (!raw.proposedEndTime) endControl.setErrors({ required: true });
      if (raw.proposedStartTime && raw.proposedEndTime && raw.proposedEndTime <= raw.proposedStartTime) {
        endControl.setErrors({ dateRange: true });
      }
      if (startControl.invalid || endControl.invalid) return;
    }

    this.savingChange.set(true);
    this.portalService
      .requestChange(lessonId, {
        type,
        proposedStartTime: raw.proposedStartTime?.toISOString() ?? null,
        proposedEndTime: raw.proposedEndTime?.toISOString() ?? null,
        reason: raw.reason || null
      })
      .subscribe({
        next: () => {
          this.savingChange.set(false);
          this.showChangeDialog.set(false);
          this.messageService.add({ severity: 'success', summary: 'הבקשה נשלחה למורה' });
        },
        error: err => {
          this.savingChange.set(false);
          this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'שליחת הבקשה נכשלה.') });
        }
      });
  }

  private load(): void {
    this.loading.set(true);
    this.portalService.myLessons(this.selectedChildId()).subscribe({
      next: lessons => {
        this.lessons.set(lessons);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
