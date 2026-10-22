import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { Observable } from 'rxjs';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { fieldError, isInvalid } from '../../../core/forms/validation-messages';
import { IsraelDatePipe } from '../../../core/i18n/israel-date.pipe';
import { EmptyStateComponent } from '../../../shared/ui/empty-state.component';
import { Note } from '../../notes/notes.models';
import { NotesService } from '../../notes/notes.service';

/** ההערות הפדגוגיות בכרטיס התלמיד: רשימה, הוספה, עריכה ומחיקה. */
@Component({
  selector: 'app-student-notes-card',
  imports: [ReactiveFormsModule, ButtonModule, CardModule, CheckboxModule, DialogModule, TagModule, TextareaModule, EmptyStateComponent, IsraelDatePipe],
  templateUrl: './student-notes-card.component.html',
  host: { class: 'block' }
})
export class StudentNotesCardComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly notesService = inject(NotesService);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  readonly studentId = input.required<string>();

  protected readonly notes = signal<Note[]>([]);
  protected readonly loading = signal(true);
  protected readonly showDialog = signal(false);
  protected readonly saving = signal(false);
  protected readonly editingId = signal<string | null>(null);
  protected readonly fieldError = fieldError;
  protected readonly isInvalid = isInvalid;

  protected readonly form = this.fb.nonNullable.group({
    content: ['', [Validators.required, Validators.maxLength(4000)]],
    visibleToStudent: [false],
    visibleToParent: [false]
  });

  ngOnInit(): void {
    this.load();
  }

  /**
   * הערה שגלויה לתלמידה גלויה תמיד גם להורה (נאכף בשרת). מסמנים גם את התיבה
   * השנייה, כדי שמצב התיבות יהיה מה שיישמר בפועל ולא הבטחה שלא תתקיים.
   */
  protected onVisibleToStudentChange(checked: boolean): void {
    if (checked) this.form.controls.visibleToParent.setValue(true);
  }

  protected openAdd(): void {
    this.editingId.set(null);
    this.form.reset({ content: '', visibleToStudent: false, visibleToParent: false });
    this.showDialog.set(true);
  }

  protected openEdit(note: Note): void {
    this.editingId.set(note.id);
    this.form.reset({ content: note.content, visibleToStudent: note.visibleToStudent, visibleToParent: note.visibleToParent });
    this.showDialog.set(true);
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue();
    const noteId = this.editingId();
    const call: Observable<unknown> = noteId
      ? this.notesService.update(noteId, raw)
      : this.notesService.create({ studentId: this.studentId(), ...raw });

    this.saving.set(true);
    call.subscribe({
      next: () => {
        this.saving.set(false);
        this.showDialog.set(false);
        this.messageService.add({ severity: 'success', summary: 'ההערה נשמרה' });
        this.load();
      },
      error: err => {
        this.saving.set(false);
        this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'שמירת ההערה נכשלה.') });
      }
    });
  }

  protected confirmDelete(note: Note): void {
    this.confirmationService.confirm({
      message: 'למחוק את ההערה? הפעולה אינה הפיכה.',
      header: 'אישור מחיקה',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'מחק',
      rejectLabel: 'ביטול',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.delete(note.id)
    });
  }

  private delete(noteId: string): void {
    this.notesService.delete(noteId).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'ההערה נמחקה' });
        this.load();
      },
      error: err =>
        this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'המחיקה נכשלה.') })
    });
  }

  private load(): void {
    this.loading.set(true);
    this.notesService.listForStudent(this.studentId()).subscribe({
      next: notes => {
        this.notes.set(notes);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
