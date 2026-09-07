import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { FormsModule } from '@angular/forms';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { EmptyStateComponent } from '../../../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../../../shared/ui/page-header.component';
import { AdminSiteFeedback } from '../admin.models';
import { AdminService } from '../admin.service';

/**
 * ההודעות שנשלחו מרצועת "בהרצה". פותח על מה שלא טופל — רשימה שמצטברת בלי סימון
 * טיפול הופכת אחרי שבוע לרשימה שאף אחד לא קורא.
 */
@Component({
  selector: 'app-admin-feedback',
  imports: [
    DatePipe,
    FormsModule,
    ButtonModule,
    CardModule,
    TagModule,
    ToggleSwitchModule,
    EmptyStateComponent,
    PageHeaderComponent
  ],
  templateUrl: './admin-feedback.component.html'
})
export class AdminFeedbackComponent implements OnInit {
  private readonly admin = inject(AdminService);
  private readonly messageService = inject(MessageService);

  protected readonly rows = signal<AdminSiteFeedback[]>([]);
  protected readonly loading = signal(true);
  protected includeHandled = false;

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.admin.listFeedback(this.includeHandled).subscribe({
      next: rows => {
        this.rows.set(rows);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: 'שגיאה',
          detail: extractErrorMessage(err, 'טעינת ההודעות נכשלה.')
        });
      }
    });
  }

  protected setHandled(row: AdminSiteFeedback, handled: boolean): void {
    this.admin.markFeedbackHandled(row.id, handled).subscribe({
      next: () => this.load(),
      error: err =>
        this.messageService.add({
          severity: 'error',
          summary: 'שגיאה',
          detail: extractErrorMessage(err, 'העדכון נכשל.')
        })
    });
  }
}
