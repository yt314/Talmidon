import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { fieldError, isInvalid } from '../../../core/forms/validation-messages';
import { GENDER_OPTIONS, Gender } from '../../../core/models/gender';
import { Note } from '../../notes/notes.models';
import { NotesService } from '../../notes/notes.service';
import { Parent } from '../../parents/parents.models';
import { ParentsService } from '../../parents/parents.service';
import { StudentDetail } from '../students.models';
import { StudentsService } from '../students.service';

@Component({
  selector: 'app-student-detail',
  imports: [
    ReactiveFormsModule,
    FormsModule,
    RouterLink,
    DatePipe,
    ButtonModule,
    CardModule,
    CheckboxModule,
    DatePickerModule,
    DialogModule,
    InputTextModule,
    SelectModule,
    TagModule,
    TextareaModule
  ],
  templateUrl: './student-detail.component.html'
})
export class StudentDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly studentsService = inject(StudentsService);
  private readonly parentsService = inject(ParentsService);
  private readonly notesService = inject(NotesService);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  private readonly studentId = this.route.snapshot.paramMap.get('id')!;

  protected readonly loading = signal(true);
  protected readonly student = signal<StudentDetail | null>(null);
  protected readonly allParents = signal<Parent[]>([]);
  protected readonly showEditDialog = signal(false);
  protected readonly saving = signal(false);
  protected readonly linkingParentId = signal<string | null>(null);
  protected readonly selectedParentToLink = signal<string | null>(null);

  protected readonly notes = signal<Note[]>([]);
  protected readonly notesLoading = signal(true);
  protected readonly showNoteDialog = signal(false);
  protected readonly savingNote = signal(false);
  protected readonly editingNoteId = signal<string | null>(null);
  protected readonly fieldError = fieldError;
  protected readonly isInvalid = isInvalid;
  protected readonly genderOptions = GENDER_OPTIONS;

  protected readonly availableParents = computed(() => {
    const linkedIds = new Set(this.student()?.parents.map(p => p.id) ?? []);
    return this.allParents().filter(p => !linkedIds.has(p.id));
  });

  protected readonly editForm = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.maxLength(200)]],
    gender: this.fb.control<Gender | null>(null, Validators.required),
    gradeLevel: ['', [Validators.maxLength(50)]],
    birthDate: this.fb.control<Date | null>(null),
    generalInfo: ['', [Validators.maxLength(4000)]],
    isActive: [true]
  });

  protected readonly noteForm = this.fb.nonNullable.group({
    content: ['', [Validators.required, Validators.maxLength(4000)]],
    visibleToStudent: [false],
    visibleToParent: [false]
  });

  ngOnInit(): void {
    this.load();
    this.loadNotes();
    this.parentsService.list().subscribe(parents => this.allParents.set(parents));
  }

  openEditDialog(): void {
    const s = this.student();
    if (!s) return;
    this.editForm.reset({
      fullName: s.fullName,
      gender: s.gender,
      gradeLevel: s.gradeLevel ?? '',
      birthDate: s.birthDate ? new Date(s.birthDate) : null,
      generalInfo: s.generalInfo ?? '',
      isActive: s.isActive
    });
    this.showEditDialog.set(true);
  }

  saveEdit(): void {
    if (this.editForm.invalid) {
      this.editForm.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    const raw = this.editForm.getRawValue();
    this.studentsService
      .update(this.studentId, {
        fullName: raw.fullName,
        gender: raw.gender,
        gradeLevel: raw.gradeLevel || null,
        birthDate: raw.birthDate ? this.toDateOnly(raw.birthDate) : null,
        generalInfo: raw.generalInfo || null,
        isActive: raw.isActive
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.showEditDialog.set(false);
          this.messageService.add({ severity: 'success', summary: 'הפרטים נשמרו' });
          this.load();
        },
        error: err => {
          this.saving.set(false);
          this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'העדכון נכשל.') });
        }
      });
  }

  confirmDelete(): void {
    this.confirmationService.confirm({
      message: 'למחוק את התלמיד? הפעולה אינה הפיכה.',
      header: 'אישור מחיקה',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'מחק',
      rejectLabel: 'ביטול',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.deleteStudent()
    });
  }

  linkSelectedParent(): void {
    const parentId = this.selectedParentToLink();
    if (!parentId) return;
    this.linkParent(parentId);
    this.selectedParentToLink.set(null);
  }

  unlinkParent(parentId: string): void {
    this.studentsService.unlinkParent(this.studentId, parentId).subscribe({
      next: () => this.load(),
      error: err =>
        this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'ביטול הקישור נכשל.') })
    });
  }

  openAddNoteDialog(): void {
    this.editingNoteId.set(null);
    this.noteForm.reset({ content: '', visibleToStudent: false, visibleToParent: false });
    this.showNoteDialog.set(true);
  }

  openEditNoteDialog(note: Note): void {
    this.editingNoteId.set(note.id);
    this.noteForm.reset({
      content: note.content,
      visibleToStudent: note.visibleToStudent,
      visibleToParent: note.visibleToParent
    });
    this.showNoteDialog.set(true);
  }

  saveNote(): void {
    if (this.noteForm.invalid) {
      this.noteForm.markAllAsTouched();
      return;
    }
    this.savingNote.set(true);
    const raw = this.noteForm.getRawValue();
    const noteId = this.editingNoteId();
    const onSuccess = (): void => {
      this.savingNote.set(false);
      this.showNoteDialog.set(false);
      this.messageService.add({ severity: 'success', summary: 'ההערה נשמרה' });
      this.loadNotes();
    };
    const onError = (err: unknown): void => {
      this.savingNote.set(false);
      this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'שמירת ההערה נכשלה.') });
    };

    if (noteId) {
      this.notesService.update(noteId, raw).subscribe({ next: onSuccess, error: onError });
    } else {
      this.notesService.create({ studentId: this.studentId, ...raw }).subscribe({ next: onSuccess, error: onError });
    }
  }

  confirmDeleteNote(note: Note): void {
    this.confirmationService.confirm({
      message: 'למחוק את ההערה? הפעולה אינה הפיכה.',
      header: 'אישור מחיקה',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'מחק',
      rejectLabel: 'ביטול',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.deleteNote(note.id)
    });
  }

  private deleteNote(noteId: string): void {
    this.notesService.delete(noteId).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'ההערה נמחקה' });
        this.loadNotes();
      },
      error: err =>
        this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'המחיקה נכשלה.') })
    });
  }

  private loadNotes(): void {
    this.notesLoading.set(true);
    this.notesService.listForStudent(this.studentId).subscribe({
      next: notes => {
        this.notes.set(notes);
        this.notesLoading.set(false);
      },
      error: () => this.notesLoading.set(false)
    });
  }

  private linkParent(parentId: string): void {
    this.linkingParentId.set(parentId);
    this.studentsService.linkParent(this.studentId, parentId).subscribe({
      next: () => {
        this.linkingParentId.set(null);
        this.load();
      },
      error: err => {
        this.linkingParentId.set(null);
        this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'קישור ההורה נכשל.') });
      }
    });
  }

  private deleteStudent(): void {
    this.studentsService.delete(this.studentId).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'התלמיד נמחק' });
        this.router.navigate(['/app/students']);
      },
      error: err =>
        this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'המחיקה נכשלה.') })
    });
  }

  private load(): void {
    this.loading.set(true);
    this.studentsService.getById(this.studentId).subscribe({
      next: student => {
        this.student.set(student);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.student.set(null);
      }
    });
  }

  private toDateOnly(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
