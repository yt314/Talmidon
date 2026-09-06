import { DOCUMENT, Component, ChangeDetectionStrategy, computed, effect, inject, input, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { MessageService } from 'primeng/api';
import { TooltipModule } from 'primeng/tooltip';
import { buildWhatsappLink } from '../whatsapp/whatsapp.util';
import { toDataURL } from 'qrcode';

/**
 * שיתוף הכרטיס הציבורי. זו הדרך של המורה להביא תלמידים, וכל עוד הקישור היה
 * משהו שצריך להרכיב ביד מכתובת הדפדפן — היא פשוט לא שיתפה אותו.
 *
 * הקוד לא נוצר מראש אלא רק כשנפתח החלון: הוא נחוץ לחלק קטן מהמורות, והספרייה
 * שמייצרת אותו נטענת לכן רק בפועל.
 */
@Component({
  selector: 'app-share-profile',
  imports: [ButtonModule, DialogModule, TooltipModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="share-bar">
      <div class="share-link" [title]="url()">
        <i class="pi pi-link"></i>
        <span>{{ url() }}</span>
      </div>
      <div class="share-actions">
        <p-button
          [label]="copied() ? 'הועתק' : 'העתקת קישור'"
          [icon]="copied() ? 'pi pi-check' : 'pi pi-copy'"
          size="small"
          [outlined]="!copied()"
          (onClick)="copy()" />
        <p-button
          icon="pi pi-whatsapp"
          severity="success"
          [outlined]="true"
          size="small"
          pTooltip="שליחה בוואטסאפ"
          tooltipPosition="top"
          ariaLabel="שליחה בוואטסאפ"
          (onClick)="shareWhatsapp()" />
        <p-button
          icon="pi pi-envelope"
          severity="secondary"
          [outlined]="true"
          size="small"
          pTooltip="שליחה במייל"
          tooltipPosition="top"
          ariaLabel="שליחה במייל"
          (onClick)="shareEmail()" />
        <p-button
          icon="pi pi-qrcode"
          severity="secondary"
          [outlined]="true"
          size="small"
          pTooltip="קוד סריקה להדפסה"
          tooltipPosition="top"
          ariaLabel="קוד סריקה להדפסה"
          (onClick)="qrOpen.set(true)" />
      </div>
    </div>

    @if (qrOpen()) {
    <p-dialog
      [visible]="true"
      (visibleChange)="qrOpen.set($event)"
      header="קוד סריקה לכרטיס שלך"
      [modal]="true"
      [draggable]="false"
      appendTo="body"
      [style]="{ width: '22rem' }">
      <div class="share-qr">
        @if (qrDataUrl(); as src) {
          <img [src]="src" alt="קוד QR לכרטיס הציבורי" />
        } @else {
          <div class="skeleton" style="width: 220px; height: 220px"></div>
        }
        <p class="m-0 text-sm text-color-secondary">
          מי שסורק מגיע ישירות לכרטיס שלך. אפשר להדפיס על מודעה או לשלוח כתמונה.
        </p>
        <p-button
          label="הורדת התמונה"
          icon="pi pi-download"
          size="small"
          [outlined]="true"
          [disabled]="!qrDataUrl()"
          (onClick)="downloadQr()" />
      </div>
    </p-dialog>
    }
  `
})
export class ShareProfileComponent {
  private readonly document = inject(DOCUMENT);
  private readonly messageService = inject(MessageService);

  readonly teacherId = input.required<string>();
  readonly teacherName = input('');

  protected readonly copied = signal(false);
  protected readonly qrOpen = signal(false);
  protected readonly qrDataUrl = signal<string | null>(null);

  /** נבנה מהמקור החי ולא מהגדרה, כדי שהקישור יהיה נכון בכל סביבה. */
  protected readonly url = computed(
    () => `${this.document.location.origin}/teachers/${this.teacherId()}`
  );

  constructor() {
    effect(() => {
      if (!this.qrOpen() || this.qrDataUrl()) return;
      // רזולוציה גבוהה מהתצוגה כדי שההדפסה תישאר חדה
      toDataURL(this.url(), { width: 600, margin: 2 })
        .then(data => this.qrDataUrl.set(data))
        .catch(() => this.fail('לא הצלחנו לייצר את קוד הסריקה.'));
    });
  }

  protected async copy(): Promise<void> {
    try {
      await this.document.defaultView!.navigator.clipboard.writeText(this.url());
      this.copied.set(true);
      // חזרה ללשונית המקורית, כדי שהכפתור לא יישאר "הועתק" לנצח
      setTimeout(() => this.copied.set(false), 2500);
    } catch {
      this.fail('הדפדפן חסם את ההעתקה. אפשר לסמן את הקישור ולהעתיק ידנית.');
    }
  }

  protected shareWhatsapp(): void {
    const link = buildWhatsappLink('', this.shareText());
    // בלי מספר יעד wa.me אינו תקף, ולכן נשלחים ישירות למסך בחירת הנמען
    const target = link ?? `https://wa.me/?text=${encodeURIComponent(this.shareText())}`;
    this.document.defaultView!.open(target, '_blank', 'noopener');
  }

  protected shareEmail(): void {
    const subject = encodeURIComponent(`שיעורים פרטיים — ${this.teacherName()}`.trim());
    const body = encodeURIComponent(this.shareText());
    this.document.defaultView!.open(`mailto:?subject=${subject}&body=${body}`, '_blank', 'noopener');
  }

  protected downloadQr(): void {
    const data = this.qrDataUrl();
    if (!data) return;
    const link = this.document.createElement('a');
    link.href = data;
    link.download = 'talmidon-qr.png';
    link.click();
  }

  private shareText(): string {
    const name = this.teacherName().trim();
    return `${name ? `${name} — שיעורים פרטיים` : 'שיעורים פרטיים'}\n${this.url()}`;
  }

  private fail(detail: string): void {
    this.messageService.add({ severity: 'error', summary: 'שגיאה', detail });
  }
}
