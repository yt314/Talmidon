import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { TableModule } from 'primeng/table';
import { TabsModule } from 'primeng/tabs';
import { TagModule } from 'primeng/tag';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { fieldError, isInvalid } from '../../../core/forms/validation-messages';
import { Parent } from '../../parents/parents.models';
import { ParentsService } from '../../parents/parents.service';
import { StudentListItem } from '../students.models';
import { StudentsService } from '../students.service';

@Component({
  selector: 'app-students-list',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    DatePickerModule,
    DialogModule,
    InputTextModule,
    MultiSelectModule,
    TableModule,
    TabsModule,
    TagModule
  ],
  templateUrl: './students-list.component.html'
})
export class StudentsListComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly studentsService = inject(StudentsService);
  private readonly parentsService = inject(ParentsService);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly students = signal<StudentListItem[]>([]);
  protected readonly parents = signal<Parent[]>([]);
  protected readonly loading = signal(true);

  protected readonly showStudentDialog = signal(false);
  protected readonly showParentDialog = signal(false);
  protected readonly savingStudent = signal(false);
  protected readonly savingParent = signal(false);
  protected readonly fieldError = fieldError;
  protected readonly isInvalid = isInvalid;

  protected readonly studentForm = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.maxLength(200)]],
    gradeLevel: ['', [Validators.maxLength(50)]],
    birthDate: this.fb.control<Date | null>(null),
    generalInfo: ['', [Validators.maxLength(4000)]],
    loginEmail: ['', [Validators.email, Validators.maxLength(256)]],
    parentIds: this.fb.control<string[]>([])
  });

  protected readonly parentForm = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    phone: ['', [Validators.maxLength(40)]]
  });

  ngOnInit(): void {
    this.loadStudents();
    this.loadParents();
  }

  openStudentDialog(): void {
    this.studentForm.reset({ fullName: '', gradeLevel: '', birthDate: null, generalInfo: '', loginEmail: '', parentIds: [] });
    this.showStudentDialog.set(true);
  }

  openParentDialog(): void {
    this.parentForm.reset({ fullName: '', email: '', phone: '' });
    this.showParentDialog.set(true);
  }

  saveStudent(): void {
    if (this.studentForm.invalid) {
      this.studentForm.markAllAsTouched();
      return;
    }
    this.savingStudent.set(true);
    const raw = this.studentForm.getRawValue();
    this.studentsService
      .create({
        fullName: raw.fullName,
        gradeLevel: raw.gradeLevel || null,
        birthDate: raw.birthDate ? this.toDateOnly(raw.birthDate) : null,
        generalInfo: raw.generalInfo || null,
        loginEmail: raw.loginEmail || null,
        parentIds: raw.parentIds
      })
      .subscribe({
        next: () => {
          this.savingStudent.set(false);
          this.showStudentDialog.set(false);
          this.messageService.add({ severity: 'success', summary: 'התלמיד נוסף בהצלחה' });
          this.loadStudents();
        },
        error: err => {
          this.savingStudent.set(false);
          this.messageService.add({
            severity: 'error',
            summary: 'שגיאה',
            detail: extractErrorMessage(err, 'הוספת התלמיד נכשלה.')
          });
        }
      });
  }

  saveParent(): void {
    if (this.parentForm.invalid) {
      this.parentForm.markAllAsTouched();
      return;
    }
    this.savingParent.set(true);
    const raw = this.parentForm.getRawValue();
    this.parentsService.create({ fullName: raw.fullName, email: raw.email, phone: raw.phone || null }).subscribe({
      next: () => {
        this.savingParent.set(false);
        this.showParentDialog.set(false);
        this.messageService.add({ severity: 'success', summary: 'ההורה נוסף בהצלחה' });
        this.loadParents();
      },
      error: err => {
        this.savingParent.set(false);
        this.messageService.add({
          severity: 'error',
          summary: 'שגיאה',
          detail: extractErrorMessage(err, 'הוספת ההורה נכשלה.')
        });
      }
    });
  }

  openStudent(student: StudentListItem): void {
    this.router.navigate(['/app/students', student.id]);
  }

  private loadStudents(): void {
    this.loading.set(true);
    this.studentsService.list().subscribe({
      next: students => {
        this.students.set(students);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  private loadParents(): void {
    this.parentsService.list().subscribe(parents => this.parents.set(parents));
  }

  private toDateOnly(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
