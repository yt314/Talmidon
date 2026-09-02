import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { fieldError, isInvalid } from '../../core/forms/validation-messages';
import { extractErrorMessage } from '../../core/http/extract-error-message';
import { ContactRequestsService } from './contact-requests.service';

/**
 * טופס פנייה בפרופיל הציבורי. עד עכשיו הספרייה הסתיימה במספר טלפון, והשיחה
 * עזבה את המערכת בלי שאיש ידע שהיא קרתה — כאן היא נכנסת למרכז ההתראות של
 * המורה ומחכה לטיפול.
 */
@Component({
  selector: 'app-contact-form',
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule, MessageModule, SelectModule, TextareaModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (sent()) {
      <div class="contact-sent">
        <i class="pi pi-check-circle"></i>
        <p class="m-0 font-medium">הפנייה נשלחה</p>
        <p class="m-0 text-sm text-color-secondary">{{ teacherName() }} תיצור איתך קשר בהקדם.</p>
      </div>
    } @else {
      <form [formGroup]="form" (ngSubmit)="submit()">
        @if (error(); as msg) {
          <p-message severity="error" [text]="msg" styleClass="w-full mb-3" />
        }

        <div class="field">
          <label for="c-name">שם מלא *</label>
          <input pInputText id="c-name" formControlName="fullName" class="w-full" [invalid]="isInvalid(form.controls.fullName)" />
          @if (fieldError(form.controls.fullName); as msg) {
            <small class="text-red-500 block mt-1">{{ msg }}</small>
          }
        </div>

        <div class="field">
          <label for="c-phone">טלפון *</label>
          <input pInputText id="c-phone" formControlName="phone" class="w-full" inputmode="tel" [invalid]="isInvalid(form.controls.phone)" />
          @if (fieldError(form.controls.phone); as msg) {
            <small class="text-red-500 block mt-1">{{ msg }}</small>
          }
        </div>

        <div class="field">
          <label for="c-email">מייל</label>
          <input pInputText id="c-email" formControlName="email" class="w-full" type="email" [invalid]="isInvalid(form.controls.email)" />
          @if (fieldError(form.controls.email); as msg) {
            <small class="text-red-500 block mt-1">{{ msg }}</small>
          }
        </div>

        @if (subjects().length > 0) {
          <div class="field">
            <label for="c-subject">באיזה תחום?</label>
            <p-select
              inputId="c-subject"
              [options]="subjects()"
              formControlName="subject"
              placeholder="בחר/י תחום"
              [showClear]="true"
              styleClass="w-full" />
          </div>
        }

        <div class="field">
          <label for="c-message">מה תרצו לשאול? *</label>
          <textarea
            pTextarea
            id="c-message"
            formControlName="message"
            rows="4"
            class="w-full"
            placeholder="כיתה, מטרת השיעורים, זמינות — כל מה שיעזור לענות לך מדויק"
            [invalid]="isInvalid(form.controls.message)"></textarea>
          @if (fieldError(form.controls.message); as msg) {
            <small class="text-red-500 block mt-1">{{ msg }}</small>
          }
        </div>

        <p-button label="שליחת פנייה" icon="pi pi-send" type="submit" [loading]="sending()" styleClass="w-full" />
        <p class="text-xs text-color-secondary mt-2 mb-0 text-center">הפרטים נשלחים למורה בלבד.</p>
      </form>
    }
  `
})
export class ContactFormComponent {
  readonly teacherId = input.required<string>();
  readonly teacherName = input('המורה');
  readonly subjects = input<string[]>([]);

  private readonly fb = inject(FormBuilder);
  private readonly service = inject(ContactRequestsService);

  protected readonly fieldError = fieldError;
  protected readonly isInvalid = isInvalid;
  protected readonly sending = signal(false);
  protected readonly sent = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.maxLength(200)]],
    phone: ['', [Validators.required, Validators.maxLength(40)]],
    email: ['', [Validators.email, Validators.maxLength(256)]],
    subject: this.fb.control<string | null>(null),
    message: ['', [Validators.required, Validators.maxLength(2000)]]
  });

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.sending.set(true);
    this.error.set(null);
    const raw = this.form.getRawValue();

    this.service
      .send(this.teacherId(), {
        fullName: raw.fullName,
        phone: raw.phone,
        email: raw.email || null,
        subject: raw.subject,
        message: raw.message
      })
      .subscribe({
        next: () => {
          this.sending.set(false);
          this.sent.set(true);
        },
        error: err => {
          this.sending.set(false);
          // 429 מגיע מהגבלת הקצב על הטופס הציבורי — הודעה מובנת במקום קוד
          this.error.set(
            err?.status === 429
              ? 'נשלחו יותר מדי פניות מהכתובת הזו. נסו שוב בעוד כמה דקות.'
              : extractErrorMessage(err, 'שליחת הפנייה נכשלה.')
          );
        }
      });
  }
}
