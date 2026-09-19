import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { DataExportService, ExportKind } from './export.service';

/**
 * הורדת הנתונים של המורה.
 *
 * קיים כי הנתונים שלה אינם אמורים להיות לכודים כאן: רשימת התשלומים נדרשת לדוח שנתי,
 * רשימת התלמידות היא גיבוי, ומי שתרצה יום אחד לעבוד אחרת צריכה לצאת עם מה שהכניסה.
 */
@Component({
  selector: 'app-data-export',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ButtonModule, CardModule],
  template: `
    <p-card>
      <h3 class="mt-0 mb-1">הנתונים שלך</h3>
      <p class="mt-0 mb-3 text-sm text-color-secondary">
        הורדה כקובץ שנפתח באקסל או בגיליון של גוגל — לגיבוי, לדוח שנתי, או פשוט כדי
        שיהיה לך עותק.
      </p>

      <div class="flex gap-2 flex-wrap">
        @for (item of items; track item.kind) {
          <p-button
            [label]="item.label"
            icon="pi pi-download"
            severity="secondary"
            [outlined]="true"
            [loading]="busy() === item.kind"
            (onClick)="download(item.kind)" />
        }
      </div>
    </p-card>
  `
})
export class DataExportComponent {
  private readonly service = inject(DataExportService);
  private readonly toast = inject(MessageService);

  protected readonly busy = signal<ExportKind | null>(null);

  protected readonly items: { kind: ExportKind; label: string }[] = [
    { kind: 'students', label: 'תלמידות' },
    { kind: 'lessons', label: 'שיעורים' },
    { kind: 'payments', label: 'תשלומים' }
  ];

  protected download(kind: ExportKind): void {
    this.busy.set(kind);
    this.service
      .download(kind)
      .catch(err =>
        this.toast.add({
          severity: 'error',
          summary: 'שגיאה',
          detail: extractErrorMessage(err, 'ההורדה נכשלה. נסי שוב.')
        })
      )
      .finally(() => this.busy.set(null));
  }
}
