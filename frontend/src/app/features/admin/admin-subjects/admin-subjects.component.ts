import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { SelectButtonModule } from 'primeng/selectbutton';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { EmptyStateComponent } from '../../../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../../../shared/ui/page-header.component';
import { AdminSubjectSuggestion } from '../admin.models';
import { AdminService } from '../admin.service';

/**
 * ניהול ההצעות לתחומי לימוד. הרשימה שמורה רואה נבנית מקטלוג קבוע ומכל מה
 * שמורות הקלידו, ולכן שגיאת הקלדה של אחת מוצעת לכולן — כאן מסתירים אותה.
 */
@Component({
  selector: 'app-admin-subjects',
  imports: [
    FormsModule,
    ButtonModule,
    CardModule,
    InputTextModule,
    SelectButtonModule,
    TagModule,
    TooltipModule,
    EmptyStateComponent,
    PageHeaderComponent
  ],
  templateUrl: './admin-subjects.component.html'
})
export class AdminSubjectsComponent implements OnInit {
  private readonly service = inject(AdminService);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly loading = signal(true);
  protected readonly all = signal<AdminSubjectSuggestion[]>([]);
  protected readonly search = signal('');
  protected readonly newName = signal('');
  protected readonly busy = signal<string | null>(null);

  /** null = הכול. */
  protected readonly filter = signal<'active' | 'hidden' | null>('active');
  protected readonly filterOptions = [
    { label: 'מוצעים', value: 'active' as const },
    { label: 'מוסתרים', value: 'hidden' as const },
    { label: 'הכול', value: null }
  ];

  protected readonly visible = computed(() => {
    const term = this.search().trim().toLowerCase();
    const mode = this.filter();
    return this.all().filter(
      row =>
        (!term || row.name.toLowerCase().includes(term)) &&
        (mode === null || (mode === 'hidden' ? row.isHidden : !row.isHidden))
    );
  });

  protected readonly hiddenCount = computed(() => this.all().filter(r => r.isHidden).length);

  ngOnInit(): void {
    this.load();
  }

  protected add(): void {
    const name = this.newName().trim();
    if (!name) return;
    this.busy.set(name);
    this.service.addSubjectSuggestion(name).subscribe({
      next: () => {
        this.newName.set('');
        this.busy.set(null);
        this.load();
        this.messageService.add({ severity: 'success', summary: 'נוסף', detail: `"${name}" יוצע מעכשיו למורות.` });
      },
      error: err => this.fail(err, 'ההוספה נכשלה.')
    });
  }

  protected hide(row: AdminSubjectSuggestion): void {
    this.confirmationService.confirm({
      message: row.isInUse
        ? `"${row.name}" כבר נבחר על ידי מורה. הסתרה מורידה אותו מרשימת ההצעות בלבד — הוא יישאר בפרופיל שלה.`
        : `להסתיר את "${row.name}" מרשימת ההצעות?`,
      header: 'הסתרת תחום',
      icon: 'pi pi-eye-slash',
      acceptLabel: 'הסתר',
      rejectLabel: 'ביטול',
      accept: () => {
        this.busy.set(row.name);
        this.service.hideSubjectSuggestion(row.name).subscribe({
          next: () => {
            this.busy.set(null);
            this.load();
          },
          error: err => this.fail(err, 'ההסתרה נכשלה.')
        });
      }
    });
  }

  /** ביטול הסתרה הוא בדיוק הוספה מחדש של אותו שם. */
  protected restore(row: AdminSubjectSuggestion): void {
    this.busy.set(row.name);
    this.service.addSubjectSuggestion(row.name).subscribe({
      next: () => {
        this.busy.set(null);
        this.load();
      },
      error: err => this.fail(err, 'השחזור נכשל.')
    });
  }

  private load(): void {
    this.loading.set(true);
    this.service.listSubjectSuggestions().subscribe({
      next: rows => {
        this.all.set(rows);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  private fail(err: unknown, fallback: string): void {
    this.busy.set(null);
    this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, fallback) });
  }
}
