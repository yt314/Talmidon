import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { CheckboxModule } from 'primeng/checkbox';
import { ChipModule } from 'primeng/chip';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { Subject, TeacherProfile } from './profile.models';
import { TeacherProfileService } from './profile.service';

@Component({
  selector: 'app-teacher-profile-settings',
  imports: [ReactiveFormsModule, FormsModule, ButtonModule, CardModule, CheckboxModule, ChipModule, InputNumberModule, InputTextModule, TextareaModule],
  templateUrl: './profile.component.html'
})
export class TeacherProfileSettingsComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly profileService = inject(TeacherProfileService);
  private readonly messageService = inject(MessageService);

  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly profile = signal<TeacherProfile | null>(null);
  protected readonly subjects = signal<Subject[]>([]);
  protected readonly newSubjectName = signal('');
  protected readonly addingSubject = signal(false);

  protected readonly form = this.fb.nonNullable.group({
    phone: [''],
    bio: [''],
    defaultPricePerLesson: [0, [Validators.required, Validators.min(0)]],
    rulesText: [''],
    contactInfo: [''],
    isPublic: [true]
  });

  ngOnInit(): void {
    this.load();
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
        bio: raw.bio || null,
        defaultPricePerLesson: raw.defaultPricePerLesson,
        rulesText: raw.rulesText || null,
        contactInfo: raw.contactInfo || null,
        isPublic: raw.isPublic
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.messageService.add({ severity: 'success', summary: 'הפרטים נשמרו' });
        },
        error: err => {
          this.saving.set(false);
          this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'השמירה נכשלה.') });
        }
      });
  }

  addSubject(): void {
    const name = this.newSubjectName().trim();
    if (!name) return;
    this.addingSubject.set(true);
    this.profileService.addSubject(name).subscribe({
      next: subject => {
        this.addingSubject.set(false);
        this.subjects.set([...this.subjects(), subject]);
        this.newSubjectName.set('');
      },
      error: err => {
        this.addingSubject.set(false);
        this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'הוספת התחום נכשלה.') });
      }
    });
  }

  removeSubject(subject: Subject): void {
    this.profileService.deleteSubject(subject.id).subscribe({
      next: () => this.subjects.set(this.subjects().filter(s => s.id !== subject.id)),
      error: err => this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'מחיקת התחום נכשלה.') })
    });
  }

  private load(): void {
    this.loading.set(true);
    this.profileService.getMyProfile().subscribe({
      next: profile => {
        this.profile.set(profile);
        this.subjects.set(profile.subjects);
        this.form.reset({
          phone: profile.phone ?? '',
          bio: profile.bio ?? '',
          defaultPricePerLesson: profile.defaultPricePerLesson,
          rulesText: profile.rulesText ?? '',
          contactInfo: profile.contactInfo ?? '',
          isPublic: profile.isPublic
        });
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
