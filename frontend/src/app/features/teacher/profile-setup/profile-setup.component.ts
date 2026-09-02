import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { fieldError, isInvalid } from '../../../core/forms/validation-messages';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { AvatarComponent } from '../../../shared/avatar/avatar.component';
import { cropToSquareJpeg } from '../../../shared/avatar/image-resize.util';
import { teacherPhotoUrl } from '../../../shared/avatar/photo-url.util';
import { TeacherProfileService } from '../profile/profile.service';
import { ProfileSetupService } from './profile-setup.service';

/**
 * מסך ההקמה שאליו מגיעה מורה שהפרופיל הציבורי שלה עדיין ריק. מבקש רק את שלושת
 * הדברים שהופכים כרטיס בספרייה לשמיש — תחומים, מחיר ודרך ליצור קשר — ומשאיר
 * את השאר (שעות זמינות, מדיניות ביטולים) למסך ההגדרות המלא.
 */
@Component({
  selector: 'app-profile-setup',
  imports: [
    ReactiveFormsModule,
    FormsModule,
    AutoCompleteModule,
    ButtonModule,
    CardModule,
    InputNumberModule,
    InputTextModule,
    TextareaModule,
    AvatarComponent
  ],
  templateUrl: './profile-setup.component.html'
})
export class ProfileSetupComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly profileService = inject(TeacherProfileService);
  private readonly setup = inject(ProfileSetupService);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly fieldError = fieldError;
  protected readonly isInvalid = isInvalid;

  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly fullName = signal('');
  private readonly teacherId = signal('');
  protected readonly photoUrl = signal<string | null>(null);
  protected readonly photoBusy = signal(false);
  protected readonly photoError = signal<string | null>(null);

  protected readonly subjectNames = signal<string[]>([]);
  protected readonly subjectSuggestions = signal<string[]>([]);
  private readonly allSuggestions = signal<string[]>([]);

  protected readonly form = this.fb.nonNullable.group({
    defaultPricePerLesson: [0, [Validators.required, Validators.min(1)]],
    contactInfo: ['', [Validators.required, Validators.maxLength(1000)]],
    bio: ['', [Validators.maxLength(2000)]]
  });

  ngOnInit(): void {
    this.profileService.subjectSuggestions().subscribe(names => this.allSuggestions.set(names));
    this.profileService.getMyProfile().subscribe({
      next: profile => {
        this.loading.set(false);
        this.fullName.set(profile.fullName);
        this.teacherId.set(profile.id);
        this.photoUrl.set(teacherPhotoUrl(profile.id, profile.photoVersion));
        this.subjectNames.set(profile.subjects.map(s => s.name));
        this.form.patchValue({
          defaultPricePerLesson: profile.defaultPricePerLesson,
          contactInfo: profile.contactInfo ?? '',
          bio: profile.bio ?? ''
        });
      },
      error: () => this.loading.set(false)
    });
  }

  filterSubjects(event: { query: string }): void {
    const query = event.query.trim().toLowerCase();
    const chosen = new Set(this.subjectNames().map(n => n.toLowerCase()));
    this.subjectSuggestions.set(
      this.allSuggestions()
        .filter(name => !chosen.has(name.toLowerCase()))
        .filter(name => !query || name.toLowerCase().includes(query))
        .slice(0, 20)
    );
  }

  async onPhotoSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
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

  save(): void {
    if (this.subjectNames().length === 0) {
      this.messageService.add({ severity: 'warn', summary: 'חסר תחום לימוד', detail: 'יש להוסיף תחום אחד לפחות.' });
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const raw = this.form.getRawValue();

    // התחומים נשמרים ראשונים: הכלל שמחליט אם הפרופיל מלא סופר אותם, ואם השמירה
    // שלהם תיכשל אין טעם לעדכן את השאר ולהציג "הפרופיל מוכן".
    this.profileService.setSubjects(this.subjectNames()).subscribe({
      next: () =>
        this.profileService
          .updateMyProfile({
            phone: null,
            bio: raw.bio || null,
            defaultPricePerLesson: raw.defaultPricePerLesson,
            defaultDurationMinutes: 60,
            rulesText: null,
            contactInfo: raw.contactInfo,
            isPublic: true
          })
          .subscribe({
            next: () => {
              this.saving.set(false);
              this.setup.refresh();
              this.messageService.add({ severity: 'success', summary: 'הפרופיל מוכן', detail: 'אפשר להתחיל להוסיף תלמידים.' });
              this.router.navigate(['/app/dashboard']);
            },
            error: err => this.fail(err)
          }),
      error: err => this.fail(err)
    });
  }

  skip(): void {
    this.setup.skip();
    this.router.navigate(['/app/dashboard']);
  }

  private fail(err: unknown): void {
    this.saving.set(false);
    this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'השמירה נכשלה.') });
  }
}
