import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TabsModule } from 'primeng/tabs';
import { TagModule } from 'primeng/tag';
import { EmptyStateComponent } from '../../../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../../../shared/ui/page-header.component';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { fieldError, isInvalid } from '../../../core/forms/validation-messages';
import { GENDER_OPTIONS, Gender } from '../../../core/models/gender';
import { getAvatarColor, getInitials } from '../../../shared/avatar/avatar.util';
import { downloadCsv } from '../../../shared/export/csv.util';
import { ContactRequest, ContactRequestStatus } from '../../contact-requests/contact-requests.models';
import { ContactRequestsService } from '../../contact-requests/contact-requests.service';
import { Parent } from '../../parents/parents.models';
import { ParentsService } from '../../parents/parents.service';
import { StudentListItem } from '../students.models';
import { StudentsService } from '../students.service';
import { RestoreFocusOnCloseDirective } from '../../../shared/a11y/restore-focus.directive';

@Component({
  selector: 'app-students-list',
  imports: [ReactiveFormsModule,
    ButtonModule,
    DatePickerModule,
    DialogModule,
    InputTextModule,
    MultiSelectModule,
    SelectModule,
    TableModule,
    TabsModule,
    TagModule, PageHeaderComponent, EmptyStateComponent, RestoreFocusOnCloseDirective],
  templateUrl: './students-list.component.html'
})
export class StudentsListComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly studentsService = inject(StudentsService);
  private readonly parentsService = inject(ParentsService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly contactRequestsService = inject(ContactRequestsService);
  private readonly messageService = inject(MessageService);

  protected readonly genderOptions = GENDER_OPTIONS;
  protected readonly initials = getInitials;
  protected readonly avatarColor = getAvatarColor;
  protected readonly students = signal<StudentListItem[]>([]);
  protected readonly parents = signal<Parent[]>([]);
  protected readonly loading = signal(true);

  protected readonly showStudentDialog = signal(false);
  protected readonly showParentDialog = signal(false);
  /** נפתח מתוך דיאלוג התלמיד — ההורה החדש ייבחר שם אוטומטית. */
  private readonly parentDialogReturnsToStudent = signal(false);
  /** הפנייה שממנה הגענו, אם הגענו מתיבת הפניות. מסומנת "בטיפול" אחרי שהתלמידה נוספה. */
  protected readonly fromContact = signal<ContactRequest | null>(null);
  /** true מרגע שטופס ההורה נפתח ועד שהמיקוד התיישב בו. ראו onParentNameFocused. */
  private readonly parentDialogSettling = signal(false);
  protected readonly savingStudent = signal(false);
  protected readonly savingParent = signal(false);
  protected readonly fieldError = fieldError;
  protected readonly isInvalid = isInvalid;

  protected readonly showEditParentDialog = signal(false);
  protected readonly editingParentId = signal<string | null>(null);
  protected readonly savingEditParent = signal(false);

  protected readonly studentForm = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.maxLength(200)]],
    gender: this.fb.control<Gender | null>(null, Validators.required),
    gradeLevel: ['', [Validators.maxLength(50)]],
    birthDate: this.fb.control<Date | null>(null),
    generalInfo: ['', [Validators.maxLength(4000)]],
    loginEmail: ['', [Validators.email, Validators.maxLength(256)]],
    parentIds: this.fb.control<string[]>([])
  });

  protected readonly parentForm = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.maxLength(200)]],
    gender: this.fb.control<Gender | null>(null, Validators.required),
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    phone: ['', [Validators.maxLength(40)]]
  });

  protected readonly editParentForm = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.maxLength(200)]],
    gender: this.fb.control<Gender | null>(null, Validators.required),
    phone: ['', [Validators.maxLength(40)]]
  });

  ngOnInit(): void {
    this.loadStudents();
    this.loadParents();

    const contactId = this.route.snapshot.queryParamMap.get('fromContact');
    if (contactId) this.startFromContact(contactId);
  }

  /**
   * הגעה מתיבת הפניות. הפרטים שכבר הוקלדו שם — שם, טלפון ומייל — הם של מי
   * שפנתה, ובמודל הזה רק להורה יש טלפון ומייל; לכן הם נכנסים לטופס ההורה,
   * וממנו ממשיכים לטופס התלמידה עם ההורה כבר מקושר.
   */
  private startFromContact(contactId: string): void {
    this.contactRequestsService.get(contactId).subscribe({
      next: contact => {
        this.fromContact.set(contact);
        // רק טופס ההורה נפתח. שני דיאלוגים בבת אחת גזלו את המיקוד זה מזה, וטופס
        // התלמידה הספיק להיות focus-then-blur — כלומר הציג "שדה חובה" עוד לפני
        // שהוקלדה בו אות.
        this.resetStudentForm();
        this.parentDialogReturnsToStudent.set(true);
        this.parentForm.reset({
          fullName: contact.fullName,
          gender: null,
          email: contact.email ?? '',
          phone: contact.phone
        });
        this.showParentDialog.set(true);
      },
      // פנייה שנמחקה בינתיים — פותחים טופס רגיל במקום להשאיר מסך שלא הגיב
      error: () => this.openStudentDialog()
    });
  }

  /** אחרי שהתלמידה נוספה: הפנייה כבר טופלה, ואין סיבה שתמשיך להופיע כ"חדשה". */
  private closeContactLoop(): void {
    const contact = this.fromContact();
    this.fromContact.set(null);
    this.router.navigate([], { relativeTo: this.route, queryParams: {} });
    if (!contact || contact.status !== ContactRequestStatus.New) return;

    this.contactRequestsService.updateStatus(contact.id, ContactRequestStatus.Handled).subscribe({
      error: () => {
        // התלמידה כבר נוספה; הפנייה תסומן ידנית. אין טעם להטריד על כך.
      }
    });
  }

  openStudentDialog(): void {
    this.resetStudentForm();
    this.showStudentDialog.set(true);
  }

  private resetStudentForm(): void {
    this.studentForm.reset({ fullName: '', gender: null, gradeLevel: '', birthDate: null, generalInfo: '', loginEmail: '', parentIds: [] });
  }

  openParentDialog(): void {
    this.parentDialogReturnsToStudent.set(false);
    this.parentForm.reset({ fullName: '', gender: null, email: '', phone: '' });
    this.showParentDialog.set(true);
  }

  /**
   * הורה חדש מתוך דיאלוג הוספת התלמיד. בלי זה המורה נאלצת לעזוב באמצע, ליצור
   * את ההורה בלשונית ההורים ולחזור — ובדרך נשכח לקשר, והשיעורים של התלמיד
   * נשארים בלי מי שישלם עליהם (ראו מסך התשלומים).
   */
  openParentDialogForStudent(): void {
    this.parentDialogReturnsToStudent.set(true);
    this.parentForm.reset({ fullName: '', gender: null, email: '', phone: '' });
    this.showParentDialog.set(true);
  }

  protected onParentDialogShown(): void {
    this.parentDialogSettling.set(true);
  }

  /**
   * טופס שעוד לא הוקלדה בו אות פתח ב"שדה חובה" באדום: מלכודת המיקוד של הדיאלוג
   * נוגעת בשדה אחד, עוברת ממנו אל שדה ה-autofocus, וה-blur שבדרך מסמן את הראשון
   * כ"נגעו" — וגם הדיאלוג שנשאר מאחור מאבד כך את המיקוד שהיה בו.
   *
   * המיקוד שמגיע לשדה השם הוא סוף אותו ריקוד, ולכן זה הרגע לנקות — ולא טיימר
   * שמנחש מתי הוא נגמר. פעם אחת לכל פתיחה, כדי שחזרה לשדה בהמשך לא תמחק סימון
   * אמיתי.
   */
  protected onParentNameFocused(): void {
    if (!this.parentDialogSettling()) return;
    this.parentDialogSettling.set(false);
    this.parentForm.markAsUntouched();
    this.studentForm.markAsUntouched();
  }

  /**
   * סגירת טופס ההורה כשהגענו מפנייה — בלחיצה על "ביטול", על ה-X או על Escape.
   * בלעדיה ויתור על יצירת ההורה היה מחזיר למסך רשימה בלי שום טופס פתוח, אחרי
   * שנלחץ "הוספה כתלמידה".
   */
  protected onParentDialogHidden(): void {
    if (this.fromContact()) this.showStudentDialog.set(true);
  }

  openEditParentDialog(parent: Parent): void {
    this.editingParentId.set(parent.id);
    this.editParentForm.reset({ fullName: parent.fullName, gender: parent.gender, phone: parent.phone ?? '' });
    this.showEditParentDialog.set(true);
  }

  saveEditParent(): void {
    const id = this.editingParentId();
    if (!id) return;
    if (this.editParentForm.invalid) {
      this.editParentForm.markAllAsTouched();
      return;
    }
    this.savingEditParent.set(true);
    const raw = this.editParentForm.getRawValue();
    this.parentsService.update(id, { fullName: raw.fullName, gender: raw.gender, phone: raw.phone || null }).subscribe({
      next: () => {
        this.savingEditParent.set(false);
        this.showEditParentDialog.set(false);
        this.messageService.add({ severity: 'success', summary: 'פרטי ההורה נשמרו' });
        this.loadParents();
      },
      error: err => {
        this.savingEditParent.set(false);
        this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'העדכון נכשל.') });
      }
    });
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
        gender: raw.gender,
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
          this.closeContactLoop();
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
    this.parentsService.create({ fullName: raw.fullName, gender: raw.gender, email: raw.email, phone: raw.phone || null }).subscribe({
      next: parent => {
        this.savingParent.set(false);
        this.showParentDialog.set(false);
        this.messageService.add({ severity: 'success', summary: 'ההורה נוסף בהצלחה' });
        // ההורה כבר ברשימה המקומית, כדי שה-multiselect יוכל להציג אותו מיד
        this.parents.set([...this.parents(), parent].sort((a, b) => a.fullName.localeCompare(b.fullName, 'he')));
        if (this.parentDialogReturnsToStudent()) {
          this.parentDialogReturnsToStudent.set(false);
          const selected = this.studentForm.controls.parentIds.value ?? [];
          this.studentForm.controls.parentIds.setValue([...selected, parent.id]);
          this.showStudentDialog.set(true);
        }
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

  exportStudents(): void {
    const rows: (string | number)[][] = [
      ['שם', 'כיתה', 'סטטוס', 'מספר הורים', 'מחיר לשיעור', 'משך (דק׳)'],
      ...this.students().map(s => [
        s.fullName,
        s.gradeLevel ?? '',
        s.isActive ? 'פעיל' : 'לא פעיל',
        s.parentCount,
        s.defaultPricePerLesson ?? '',
        s.defaultDurationMinutes ?? ''
      ])
    ];
    downloadCsv('תלמידים.csv', rows);
    this.messageService.add({ severity: 'success', summary: 'הקובץ יורד' });
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
