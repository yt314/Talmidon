import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DatePickerModule } from 'primeng/datepicker';
import { SkeletonModule } from 'primeng/skeleton';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { SpotlightDirective } from '../../shared/ui/spotlight.directive';
import { StatCardComponent } from '../../shared/ui/stat-card.component';
import { downloadCsv } from '../../shared/export/csv.util';
import { IncomeReport } from './reports.models';
import { ReportsService } from './reports.service';

@Component({
  selector: 'app-reports',
  imports: [FormsModule, ButtonModule, CardModule, DatePickerModule, SkeletonModule, PageHeaderComponent, StatCardComponent, SpotlightDirective],
  templateUrl: './reports.component.html'
})
export class ReportsComponent implements OnInit {
  private readonly reportsService = inject(ReportsService);
  private readonly messageService = inject(MessageService);

  protected readonly month = signal<Date>(new Date());
  protected readonly report = signal<IncomeReport | null>(null);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    this.load();
  }

  onMonthChange(date: Date | null): void {
    if (!date) return;
    this.month.set(date);
    this.load();
  }

  private load(): void {
    const date = this.month();
    this.loading.set(true);
    this.reportsService.incomeReport(date.getFullYear(), date.getMonth() + 1).subscribe({
      next: report => {
        this.report.set(report);
        this.loading.set(false);
      },
      error: () => {
        this.report.set(null);
        this.loading.set(false);
      }
    });
  }

  exportCsv(): void {
    const report = this.report();
    if (!report) return;

    const rows: (string | number)[][] = [
      ['תלמיד', 'שיעורים', 'חויב (₪)', 'שולם (₪)', 'נותר (₪)'],
      ...report.byStudent.map(s => [s.studentName, s.lessons, s.charged, s.paid, s.charged - s.paid]),
      ['סה״כ', report.completedLessons, report.totalCharged, report.totalPaid, report.totalOutstanding]
    ];
    const name = `דוח-הכנסות-${report.year}-${String(report.month).padStart(2, '0')}.csv`;
    downloadCsv(name, rows);
    this.messageService.add({ severity: 'success', summary: 'הקובץ יורד' });
  }
}
