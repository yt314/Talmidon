import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { LESSON_STATUS_LABELS, ChangeRequestType, Lesson, LessonStatus } from '../../lessons/lessons.models';
import { MyChild } from '../parent-portal.models';
import { ParentPortalService } from '../parent-portal.service';

@Component({
  selector: 'app-parent-lessons',
  imports: [ReactiveFormsModule, FormsModule, DatePipe, ButtonModule, DatePickerModule, DialogModule, InputTextModule, SelectModule, TableModule, TagModule],
  templateUrl: './parent-lessons.component.html'
})
export class ParentLessonsComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly portalService = inject(ParentPortalService);
  private readonly messageService = inject(MessageService);

  protected readonly LessonStatus = LessonStatus;
  protected readonly ChangeRequestType = ChangeRequestType;
  protected readonly statusLabel = (status: LessonStatus): string => LESSON_STATUS_LABELS[status];

  protected readonly children = signal<MyChild[]>([]);
  protected readonly selectedChildId = signal<string | null>(null);
  protected readonly lessons = signal<Lesson[]>([]);
  protected readonly loading = signal(true);

  protected readonly showRequestDialog = signal(false);
  protected readonly savingRequest = signal(false);

  protected readonly showChangeDialog = signal(false);
  protected readonly changeRequestType = signal<ChangeRequestType>(ChangeRequestType.Cancel);
  protected readonly changingLessonId = signal<string | null>(null);
  protected readonly savingChange = signal(false);

  protected readonly requestForm = this.fb.nonNullable.group({
    studentId: ['', [Validators.required]],
    startTime: this.fb.control<Date | null>(null, Validators.required),
    endTime: this.fb.control<Date | null>(null, Validators.required),
    reason: ['']
  });

  protected readonly changeForm = this.fb.nonNullable.group({
    proposedStartTime: this.fb.control<Date | null>(null),
    proposedEndTime: this.fb.control<Date | null>(null),
    reason: ['']
  });

  ngOnInit(): void {
    this.portalService.myChildren().subscribe(children => this.children.set(children));
    this.load();
  }

  onChildChange(): void {
    this.load();
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

    if (type === ChangeRequestType.Reschedule && (!raw.proposedStartTime || !raw.proposedEndTime)) {
      this.messageService.add({ severity: 'warn', summary: 'יש לבחור מועד מוצע' });
      return;
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
