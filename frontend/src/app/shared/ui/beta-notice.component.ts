import { Component, inject, signal } from '@angular/core';
import { DOCUMENT } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { environment } from '../../../environments/environment';
import { extractErrorMessage } from '../../core/http/extract-error-message';

/**
 * רצועת "בהרצה" עם טופס לשליחת רעיון או תקלה.
 *
 * פתוח גם למי שאינו מחובר — מי שנתקל בתקלה במסך ההתחברות הוא בדיוק מי שהכי כדאי לשמוע
 * ממנו. פרטי קשר אינם חובה: דרישה כזו הייתה מסננת את הדיווחים המהירים.
 *
 * כתובת הדף נשלחת יחד עם ההודעה, כדי שדיווח על תקלה יהיה שווה משהו בלי סבב שאלות חוזר.
 */
@Component({
  selector: 'app-beta-notice',
  imports: [FormsModule, ButtonModule, DialogModule, InputTextModule, TextareaModule],
  template: `
    <div class="beta-notice">
      <span class="beta-badge">בהרצה</span>
      <span class="beta-text">האתר עדיין בהרצה — יש רעיון או משהו שלא עובד?</span>
      <button type="button" class="link-button" (click)="open()">שלחו לנו הודעה</button>
    </div>

    <p-dialog
      [(visible)]="visible"
      header="הודעה לצוות תלמידון"
      [modal]="true"
      [draggable]="false"
      [dismissableMask]="true"
      appendTo="body"
      [style]="{ width: 'min(30rem, 92vw)' }"
    >
      @if (sent()) {
        <div class="text-center py-3">
          <i class="pi pi-check-circle" style="font-size: 2rem; color: var(--p-primary-color)"></i>
          <p class="mt-2 mb-0 font-medium">ההודעה נשלחה. תודה!</p>
          <p class="mt-1 mb-0 text-sm" style="color: var(--p-text-muted-color)">זה בדיוק מה שעוזר לנו לשפר.</p>
        </div>
        <div class="flex justify-content-end mt-3">
          <p-button label="סגירה" (onClick)="visible = false" />
        </div>
      } @else {
      <p class="mt-0 text-sm" style="color: var(--p-text-muted-color)">
        רעיון, תקלה או הערה — הכול עוזר. אפשר גם בלי להשאיר פרטים.
      </p>

      <label for="beta-message" class="block mb-1 font-medium">ההודעה *</label>
      <textarea
        pTextarea
        id="beta-message"
        rows="5"
        class="w-full"
        maxlength="4000"
        [(ngModel)]="message"
        placeholder="מה תרצו לספר לנו?"
      ></textarea>

      <label for="beta-contact" class="block mt-3 mb-1 font-medium">טלפון או מייל (לא חובה)</label>
      <input
        pInputText
        id="beta-contact"
        class="w-full"
        maxlength="256"
        [(ngModel)]="contactInfo"
        placeholder="אם תרצו שנחזור אליכם"
      />

      @if (error(); as err) {
        <small class="block mt-2" style="color: var(--p-red-500)">{{ err }}</small>
      }

      <div class="flex justify-content-end gap-2 mt-4">
        <p-button label="ביטול" severity="secondary" [text]="true" (onClick)="visible = false" />
        <p-button
          label="שליחה"
          icon="pi pi-send"
          [loading]="sending()"
          [disabled]="!message.trim()"
          (onClick)="send()"
        />
      </div>
      }
    </p-dialog>
  `,
  styles: [
    `
      .beta-notice {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        justify-content: center;
        gap: 0.5rem;
        padding: 0.75rem 1rem;
        font-size: 0.875rem;
        color: var(--p-text-muted-color);
      }
      .beta-badge {
        padding: 0.1rem 0.5rem;
        border-radius: 999px;
        font-weight: 600;
        font-size: 0.75rem;
        color: var(--p-primary-contrast-color);
        background: var(--p-primary-color);
      }
    `
  ]
})
export class BetaNoticeComponent {
  private readonly http = inject(HttpClient);
  private readonly document = inject(DOCUMENT);

  protected visible = false;
  protected message = '';
  protected contactInfo = '';
  protected readonly sending = signal(false);
  protected readonly sent = signal(false);
  protected readonly error = signal<string | null>(null);

  protected open(): void {
    this.sent.set(false);
    this.error.set(null);
    this.visible = true;
  }

  protected send(): void {
    const text = this.message.trim();
    if (!text) return;

    this.sending.set(true);
    this.error.set(null);
    this.http
      .post<void>(`${environment.apiUrl}/public/feedback`, {
        message: text,
        contactInfo: this.contactInfo.trim() || null,
        pageUrl: this.document.location.href
      })
      .subscribe({
        next: () => {
          this.sending.set(false);
          this.sent.set(true);
          this.message = '';
          this.contactInfo = '';
        },
        error: err => {
          this.sending.set(false);
          this.error.set(extractErrorMessage(err, 'שליחת ההודעה נכשלה. נסו שוב בעוד רגע.'));
        }
      });
  }
}
