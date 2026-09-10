import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { CheckboxModule } from 'primeng/checkbox';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SkeletonModule } from 'primeng/skeleton';
import { TextareaModule } from 'primeng/textarea';
import { PageHeaderComponent } from '../../../shared/ui/page-header.component';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { fieldError, isInvalid } from '../../../core/forms/validation-messages';
import { AvatarComponent } from '../../../shared/avatar/avatar.component';
import { SubjectPickerComponent } from '../../../shared/ui/subject-picker.component';
import { cropToSquareJpeg } from '../../../shared/avatar/image-resize.util';
import { HEBREW_DAY_NAMES } from '../../../core/i18n/primeng-hebrew';
import { ShareProfileComponent } from '../../../shared/ui/share-profile.component';
import { teacherPhotoUrl } from '../../../shared/avatar/photo-url.util';
import { AvailabilityWindow, TeacherProfile } from './profile.models';
import { TeacherProfileService } from './profile.service';
import { ProfileSetupService } from '../profile-setup/profile-setup.service';

@Component({
  selector: 'app-teacher-profile-settings',
  imports: [
    ReactiveFormsModule,
    FormsModule,
    ButtonModule,
    CardModule,
    CheckboxModule,
    InputNumberModule,
    InputTextModule,
    SkeletonModule,
    TextareaModule,
    PageHeaderComponent,
    AvatarComponent,
    SubjectPickerComponent
  , ShareProfileComponent],
  templateUrl: './profile.component.html'
})
export class TeacherProfileSettingsComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly profileService = inject(TeacherProfileService);
  private readonly setup = inject(ProfileSetupService);
  private readonly messageService = inject(MessageService);

  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly profile = signal<TeacherProfile | null>(null);
  /** שמות התחומים כפי שהם נערכים כרגע — ה-autocomplete עובד על מחרוזות. */
  protected readonly subjectNames = signal<string[]>([]);
  protected readonly allSuggestions = signal<string[]>([]);
  protected readonly subjectsError = signal<string | null>(null);
  protected readonly subjectsSaved = signal(false);
  private readonly teacherId = signal('');
  protected readonly photoUrl = signal<string | null>(null);
  protected readonly photoBusy = signal(false);
  protected readonly photoError = signal<string | null>(null);
  /** null עד שהפרופיל נטען — מונע הבהוב של באנר "הפרופיל חסר". */
  protected readonly profileComplete = signal<boolean | null>(null);
  protected readonly fieldError = fieldError;
  protected readonly isInvalid = isInvalid;

  protected readonly form = this.fb.nonNullable.group({
    phone: ['', [Validators.maxLength(40)]],
    contactEmail: ['', [Validators.email, Validators.maxLength(256)]],
    city: ['', [Validators.maxLength(100)]],
    neighborhood: ['', [Validators.maxLength(100)]],
    bio: ['', [Validators.maxLength(2000)]],
    defaultPricePerLesson: [0, [Validators.required, Validators.min(0)]],
    // אופציונליים: ריק פירושו "לא הוגדר", והמחיר ייגזר יחסית מהמחיר לשעה
    pricePer45Minutes: [null as number | null, [Validators.min(0)]],
    pricePer30Minutes: [null as number | null, [Validators.min(0)]],
    defaultDurationMinutes: [60, [Validators.required, Validators.min(1), Validators.max(1440)]],
    rulesText: ['', [Validators.maxLength(4000)]],
    isPublic: [true],
    acceptingStudents: [true]
  });

  protected readonly dayNames = HEBREW_DAY_NAMES;
  protected readonly days = [0, 1, 2, 3, 4, 5, 6];
  protected readonly availability = signal<AvailabilityWindow[]>([]);
  protected readonly savingAvailability = signal(false);

  ngOnInit(): void {
    this.profileService.subjectSuggestions().subscribe(names => this.allSuggestions.set(names));
    this.load();
    this.profileService.getAvailability().subscribe(windows => this.availability.set(windows));
  }

  windowsForDay(day: number): AvailabilityWindow[] {
    return this.availability().filter(w => w.dayOfWeek === day);
  }

  addWindow(day: number): void {
    this.availability.set([...this.availability(), { dayOfWeek: day, startTime: '09:00', endTime: '10:00' }]);
  }

  removeWindow(win: AvailabilityWindow): void {
    this.availability.set(this.availability().filter(w => w !== win));
  }

  saveAvailability(): void {
    if (this.availability().some(w => w.endTime <= w.startTime)) {
      this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: 'בכל חלון, שעת הסיום חייבת להיות אחרי שעת ההתחלה.' });
      return;
    }
    this.savingAvailability.set(true);
    this.profileService.updateAvailability(this.availability()).subscribe({
      next: () => {
        this.savingAvailability.set(false);
        this.messageService.add({ severity: 'success', summary: 'שעות הזמינות נשמרו' });
      },
      error: err => {
        this.savingAvailability.set(false);
        this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'השמירה נכשלה.') });
      }
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue();
    this.saving.set(true);
    this.profileService
      .updateMyProfile({
        phone: raw.phone || null,
        contactEmail: raw.contactEmail || null,
        city: raw.city || null,
        neighborhood: raw.neighborhood || null,
        bio: raw.bio || null,
        defaultPricePerLesson: raw.defaultPricePerLesson,
        pricePer45Minutes: raw.pricePer45Minutes,
        pricePer30Minutes: raw.pricePer30Minutes,
        defaultDurationMinutes: raw.defaultDurationMinutes,
        rulesText: raw.rulesText || null,
        isPublic: raw.isPublic,
        acceptingStudents: raw.acceptingStudents
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          // הטלפון והמייל נספרים בשלמות הפרופיל, אז המצב נבדק מחדש אחרי שמירה
          this.refreshCompleteness();
          this.messageService.add({ severity: 'success', summary: 'הפרטים נשמרו' });
        },
        error: err => {
          this.saving.set(false);
          this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'השמירה נכשלה.') });
        }
      });
  }

  // ===== תמונת פרופיל =====

  /**
   * התמונה נחתכת לריבוע ומוקטנת בדפדפן לפני ההעלאה, כדי שהשרת לא יצטרך ספריית
   * עיבוד תמונה ושמה שנשמר יהיה כבר בגודל התצוגה.
   */
  async onPhotoSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    // מאפסים מיד כדי שבחירה חוזרת של אותו קובץ תפעיל את האירוע שוב
    input.value = '';
    if (!file) return;

    if (!file.type.startsWith('image/')) {
      this.photoError.set('יש לבחור קובץ תמונה.');
      return;
    }

    this.photoError.set(null);
    this.photoBusy.set(true);
    try {
      const square = await cropToSquareJpeg(file);
      this.profileService.uploadPhoto(square).subscribe({
        next: result => {
          this.photoBusy.set(false);
          this.photoUrl.set(teacherPhotoUrl(this.teacherId(), result.photoVersion));
          this.messageService.add({ severity: 'success', summary: 'התמונה עודכנה' });
        },
        error: err => {
          this.photoBusy.set(false);
          this.photoError.set(extractErrorMessage(err, 'העלאת התמונה נכשלה.'));
        }
      });
    } catch {
      this.photoBusy.set(false);
      this.photoError.set('לא הצלחנו לקרוא את הקובץ. נסי תמונה אחרת.');
    }
  }

  removePhoto(): void {
    this.photoBusy.set(true);
    this.profileService.deletePhoto().subscribe({
      next: () => {
        this.photoBusy.set(false);
        this.photoUrl.set(null);
      },
      error: err => {
        this.photoBusy.set(false);
        this.photoError.set(extractErrorMessage(err, 'מחיקת התמונה נכשלה.'));
      }
    });
  }

  /** שולף מחדש את מצב השלמות מהשרת, שהוא מקור האמת היחיד לכלל הזה. */
  private refreshCompleteness(): void {
    this.profileService.getMyProfile().subscribe({
      next: profile => this.setCompleteness(profile.isProfileComplete),
      error: () => undefined
    });
  }

  /**
   * מעדכן גם את המטמון המשותף. השומר של ‎/app‎ קורא אותו, ובלי העדכון מורה
   * שהשלימה את הפרופיל דווקא מכאן הייתה מוחזרת למסך ההקמה בניווט הבא.
   */
  private setCompleteness(value: boolean): void {
    this.profileComplete.set(value);
    this.setup.setComplete(value);
  }

  /**
   * נשמר מיד עם כל שינוי ולא בכפתור נפרד — כך התנהג המסך גם קודם (הוספה ומחיקה
   * שמרו מיידית), ותיבת התגיות לא מזמינה "שמור" נפרד.
   */
  onSubjectsChange(names: string[]): void {
    const cleaned = names.map(n => n.trim()).filter(Boolean);
    const tooLong = cleaned.find(n => n.length > 100);
    if (tooLong) {
      this.subjectsError.set('שם תחום ארוך מדי (עד 100 תווים).');
      return;
    }

    this.subjectNames.set(cleaned);
    this.subjectsError.set(null);
    this.subjectsSaved.set(false);

    this.profileService.setSubjects(cleaned).subscribe({
      next: () => {
        this.subjectsSaved.set(true);
        this.profileComplete.set(null);
        this.refreshCompleteness();
      },
      error: err => this.subjectsError.set(extractErrorMessage(err, 'שמירת התחומים נכשלה.'))
    });
  }


  private load(): void {
    this.loading.set(true);
    this.profileService.getMyProfile().subscribe({
      next: profile => {
        this.profile.set(profile);
        this.subjectNames.set(profile.subjects.map(s => s.name));
        this.teacherId.set(profile.id);
        this.photoUrl.set(teacherPhotoUrl(profile.id, profile.photoVersion));
        this.setCompleteness(profile.isProfileComplete);
        this.form.reset({
          phone: profile.phone ?? '',
          contactEmail: profile.contactEmail ?? '',
          city: profile.city ?? '',
          neighborhood: profile.neighborhood ?? '',
          bio: profile.bio ?? '',
          defaultPricePerLesson: profile.defaultPricePerLesson,
          pricePer45Minutes: profile.pricePer45Minutes,
          pricePer30Minutes: profile.pricePer30Minutes,
          defaultDurationMinutes: profile.defaultDurationMinutes,
          rulesText: profile.rulesText ?? '',
          isPublic: profile.isPublic,
          acceptingStudents: profile.acceptingStudents
        });
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
