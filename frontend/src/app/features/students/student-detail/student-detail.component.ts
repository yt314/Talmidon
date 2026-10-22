
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { TooltipModule } from 'primeng/tooltip';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { fieldError, isInvalid } from '../../../core/forms/validation-messages';
import { GENDER_OPTIONS, Gender } from '../../../core/models/gender';
import { getAvatarColor, getInitials } from '../../../shared/avatar/avatar.util';
import { EmptyStateComponent } from '../../../shared/ui/empty-state.component';
import { buildMailtoLink, hasEmail } from '../../../shared/mail/mailto.util';
import { buildWhatsappLink, hasWhatsapp } from '../../../shared/whatsapp/whatsapp.util';
import { Parent } from '../../parents/parents.models';
import { ParentsService } from '../../parents/parents.service';
import { ParentSummary, StudentDetail } from '../students.models';
import { StudentsService } from '../students.service';
import { IsraelDatePipe } from '../../../core/i18n/israel-date.pipe';
import { RestoreFocusOnCloseDirective } from '../../../shared/a11y/restore-focus.directive';
import { StudentNotesCardComponent } from '../student-notes-card/student-notes-card.component';
import { StudentResourcesCardComponent } from '../student-resources-card/student-resources-card.component';
import { toDateOnly } from '../../../shared/dates/date-only.util';

@Component({
  selector: 'app-student-detail',
  imports: [ ReactiveFormsModule,
    FormsModule,
    RouterLink,
    ButtonModule,
    CardModule,
    CheckboxModule,
    DatePickerModule,
    DialogModule,
    InputNumberModule,
    InputTextModule,
    SelectModule,
    TagModule,
    TextareaModule,
    TooltipModule,
    EmptyStateComponent,
    IsraelDatePipe, RestoreFocusOnCloseDirective, StudentNotesCardComponent, StudentResourcesCardComponent],
  templateUrl: './student-detail.component.html'
})
export class StudentDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly studentsService = inject(StudentsService);
  private readonly parentsService = inject(ParentsService);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly initials = getInitials;
  protected readonly avatarColor = getAvatarColor;

  protected readonly studentId = this.route.snapshot.paramMap.get('id')!;

  protected readonly loading = signal(true);
  protected readonly student = signal<StudentDetail | null>(null);
  protected readonly allParents = signal<Parent[]>([]);
  protected readonly showEditDialog = signal(false);
  protected readonly saving = signal(false);
  protected readonly linkingParentId = signal<string | null>(null);
  protected readonly selectedParentToLink = signal<string | null>(null);

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
    defaultPricePerLesson: this.fb.control<number | null>(null, [Validators.min(0)]),
    defaultDurationMinutes: this.fb.control<number | null>(null, [Validators.min(1), Validators.max(1440)]),
    isActive: [true]
  });

  ngOnInit(): void {
    this.load();
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
      defaultPricePerLesson: s.defaultPricePerLesson,
      defaultDurationMinutes: s.defaultDurationMinutes,
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
        birthDate: raw.birthDate ? toDateOnly(raw.birthDate) : null,
        generalInfo: raw.generalInfo || null,
        defaultPricePerLesson: raw.defaultPricePerLesson,
        defaultDurationMinutes: raw.defaultDurationMinutes,
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

  /** קביעת שיעור לתלמיד זה — פותח את היומן עם התלמיד נבחר מראש. */
  scheduleLesson(): void {
    this.router.navigate(['/app/lessons'], { queryParams: { studentId: this.studentId } });
  }

  protected readonly hasWhatsapp = hasWhatsapp;
  protected readonly hasEmail = hasEmail;

  /** פותח שיחת וואטסאפ עם ההורה, עם הודעת פתיחה מוכנה. */
  openWhatsapp(parent: ParentSummary): void {
    const studentName = this.student()?.fullName ?? '';
    const text = `שלום ${parent.fullName}, אני המורה של ${studentName}.`;
    const link = buildWhatsappLink(parent.phone, text);
    if (link) window.open(link, '_blank', 'noopener');
  }

  /** אותה פנייה, בדואר — להורים שלא זמינים בוואטסאפ. */
  openParentEmail(parent: ParentSummary): void {
    const studentName = this.student()?.fullName ?? '';
    const link = buildMailtoLink(
      parent.email,
      studentName ? `בנוגע ל${studentName}` : 'הודעה מהמורה',
      `שלום ${parent.fullName},\n\nאני המורה של ${studentName}.\n\n`
    );
    if (link) window.open(link, '_blank', 'noopener');
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

}
