import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { IsraelDatePipe } from '../../../core/i18n/israel-date.pipe';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { EmptyStateComponent } from '../../../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../../../shared/ui/page-header.component';
import { AdminError, AdminErrorDetail } from '../admin.models';
import { AdminService } from '../admin.service';

/**
 * התקלות שנרשמו בשרת.
 *
 * הקיום של המסך הזה הוא הנקודה: היומן של ספק האירוח אינו נגיש מתוך המוצר, ולכן עד
 * עכשיו הדרך היחידה לאבחן משהו שנשבר אצל משתמשת הייתה לנחש, לפרוס, ולבדוק אם עזר.
 * כאן מגיעים לאותה תקלה לפי הקוד שהיא ראתה על המסך.
 */
@Component({
  selector: 'app-admin-errors',
  imports: [
    FormsModule,
    ButtonModule,
    CardModule,
    InputTextModule,
    TagModule,
    ToggleSwitchModule,
    EmptyStateComponent,
    PageHeaderComponent,
    IsraelDatePipe
  ],
  templateUrl: './admin-errors.component.html'
})
export class AdminErrorsComponent implements OnInit {
  private readonly admin = inject(AdminService);
  private readonly messageService = inject(MessageService);

  protected readonly rows = signal<AdminError[]>([]);
  protected readonly loading = signal(true);

  /** התקלה הפתוחה כרגע, עם עקבת המחסנית. נטענת לפי דרישה. */
  protected readonly openId = signal<string | null>(null);
  protected readonly openDetail = signal<AdminErrorDetail | null>(null);

  protected includeHandled = false;
  protected search = '';

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.openId.set(null);
    this.openDetail.set(null);

    this.admin.listErrors(this.includeHandled, this.search).subscribe({
      next: rows => {
        this.rows.set(rows);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.fail(err, 'טעינת התקלות נכשלה.');
      }
    });
  }

  protected clearSearch(): void {
    this.search = '';
    this.load();
  }

  protected toggle(row: AdminError): void {
    if (this.openId() === row.id) {
      this.openId.set(null);
      this.openDetail.set(null);
      return;
    }

    this.openId.set(row.id);
    this.openDetail.set(null);
    this.admin.getError(row.id).subscribe({
      next: detail => this.openDetail.set(detail),
      error: err => this.fail(err, 'טעינת פרטי התקלה נכשלה.')
    });
  }

  protected setHandled(row: AdminError, handled: boolean): void {
    this.admin.markErrorHandled(row.id, handled).subscribe({
      next: () => this.load(),
      error: err => this.fail(err, 'העדכון נכשל.')
    });
  }

  /** עותק אחד לקובץ שגיאות: את עקבת המחסנית מעבירים הלאה, לא מקלידים מחדש. */
  protected copy(detail: AdminErrorDetail): void {
    const text = [
      `קוד תקלה: ${detail.reference}`,
      `מתי: ${detail.occurredAt}`,
      `מה: ${detail.kind} — ${detail.message}`,
      `איפה: ${detail.method ?? ''} ${detail.path ?? ''}`.trim(),
      detail.userEmail ? `מי: ${detail.userEmail}` : '',
      '',
      detail.detail ?? ''
    ]
      .filter(line => line !== '')
      .join('\n');

    navigator.clipboard.writeText(text).then(
      () => this.messageService.add({ severity: 'success', summary: 'הועתק', detail: 'פרטי התקלה בלוח.' }),
      () => this.messageService.add({ severity: 'warn', summary: 'ההעתקה נכשלה', detail: 'סמני את הטקסט והעתיקי ידנית.' })
    );
  }

  private fail(err: unknown, fallback: string): void {
    this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, fallback) });
  }
}
