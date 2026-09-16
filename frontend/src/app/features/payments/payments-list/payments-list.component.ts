
import { DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TabsModule } from 'primeng/tabs';
import { TagModule } from 'primeng/tag';
import { EmptyStateComponent } from '../../../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../../../shared/ui/page-header.component';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { fieldError, isInvalid } from '../../../core/forms/validation-messages';
import { getAvatarColor, getInitials } from '../../../shared/avatar/avatar.util';
import { Parent } from '../../parents/parents.models';
import { ParentsService } from '../../parents/parents.service';
import { OpenCharge, OpenChargeSummary, Payment } from '../payments.models';
import { PaymentsService } from '../payments.service';
import { IsraelDatePipe } from '../../../core/i18n/israel-date.pipe';

@Component({
  selector: 'app-payments-list',
  imports: [ ReactiveFormsModule,
    FormsModule,
    ButtonModule,
    CheckboxModule,
    DatePickerModule,
    InputTextModule,
    SelectModule,
    TableModule,
    TabsModule,
    TagModule, PageHeaderComponent, EmptyStateComponent, IsraelDatePipe, DecimalPipe],
  templateUrl: './payments-list.component.html'
})
export class PaymentsListComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly paymentsService = inject(PaymentsService);
  private readonly parentsService = inject(ParentsService);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly initials = getInitials;
  protected readonly avatarColor = getAvatarColor;

  protected readonly parents = signal<Parent[]>([]);
  /** "מי חייב כמה" — התמונה שנפתחת ראשונה, לפני שבוחרים הורה מסוים. */
  protected readonly summary = signal<OpenChargeSummary[]>([]);
  protected readonly summaryLoading = signal(true);
  protected readonly selectedParentId = signal<string | null>(null);
  protected readonly openCharges = signal<OpenCharge[]>([]);
  protected readonly openChargesLoading = signal(false);
  protected readonly selectedLessonIds = signal<Set<string>>(new Set());
  protected readonly saving = signal(false);
  protected readonly sendingReminders = signal(false);

  protected readonly payments = signal<Payment[]>([]);
  protected readonly paymentsLoading = signal(true);
  protected readonly noSelectionError = signal(false);
  protected readonly fieldError = fieldError;
  protected readonly isInvalid = isInvalid;

  protected readonly totalOwed = computed(() => this.summary().reduce((sum, row) => sum + row.total, 0));
  protected readonly totalOpenLessons = computed(() => this.summary().reduce((sum, row) => sum + row.lessonCount, 0));

  protected readonly selectedTotal = computed(() => {
    const ids = this.selectedLessonIds();
    return this.openCharges()
      .filter(c => ids.has(c.lessonId))
      .reduce((sum, c) => sum + c.amount, 0);
  });

  protected readonly paymentForm = this.fb.nonNullable.group({
    paidDate: this.fb.nonNullable.control<Date>(new Date(), Validators.required),
    method: ['', [Validators.maxLength(50)]],
    note: ['', [Validators.maxLength(1000)]]
  });

  ngOnInit(): void {
    this.parentsService.list().subscribe(parents => this.parents.set(parents));
    this.loadSummary();
    this.loadPayments();
  }

  private loadSummary(): void {
    this.summaryLoading.set(true);
    this.paymentsService.openChargesSummary().subscribe({
      next: rows => {
        this.summary.set(rows);
        this.summaryLoading.set(false);
      },
      error: () => this.summaryLoading.set(false)
    });
  }

  /** לחיצה על שורה בטבלת החוב — פותחת את החיובים של אותו הורה. */
  openParent(row: OpenChargeSummary): void {
    if (!row.parentId) return;
    this.selectedParentId.set(row.parentId);
    this.onParentChange();
  }

  backToSummary(): void {
    this.selectedParentId.set(null);
    this.onParentChange();
  }

  /** כמה ימים עברו מהשיעור הפתוח הוותיק ביותר — חוב של חודש נראה אחרת מחוב של אתמול. */
  daysWaiting(isoDate: string): number {
    const days = Math.floor((Date.now() - new Date(isoDate).getTime()) / 86_400_000);
    return days > 0 ? days : 0;
  }

  onParentChange(): void {
    this.selectedLessonIds.set(new Set());
    this.noSelectionError.set(false);
    const parentId = this.selectedParentId();
    if (!parentId) {
      this.openCharges.set([]);
      return;
    }
    this.openChargesLoading.set(true);
    this.paymentsService.openCharges(parentId).subscribe({
      next: charges => {
        this.openCharges.set(charges);
        // המורה נכנסה כדי לגבות — הכול מסומן, והיא מורידה סימון מהחריגים
        this.selectedLessonIds.set(new Set(charges.map(c => c.lessonId)));
        this.openChargesLoading.set(false);
      },
      error: () => this.openChargesLoading.set(false)
    });
  }

  isSelected(lessonId: string): boolean {
    return this.selectedLessonIds().has(lessonId);
  }

  toggleLesson(lessonId: string): void {
    const next = new Set(this.selectedLessonIds());
    if (next.has(lessonId)) next.delete(lessonId);
    else next.add(lessonId);
    this.selectedLessonIds.set(next);
    this.noSelectionError.set(false);
  }

  toggleAll(): void {
    const all = this.openCharges();
    this.selectedLessonIds.set(this.selectedLessonIds().size === all.length ? new Set() : new Set(all.map(c => c.lessonId)));
    this.noSelectionError.set(false);
  }

  markAsPaid(): void {
    const parentId = this.selectedParentId();
    const lessonIds = Array.from(this.selectedLessonIds());
    this.noSelectionError.set(lessonIds.length === 0);
    if (this.paymentForm.invalid) {
      this.paymentForm.markAllAsTouched();
    }
    if (!parentId || lessonIds.length === 0 || this.paymentForm.invalid) return;

    const raw = this.paymentForm.getRawValue();
    this.saving.set(true);
    this.paymentsService
      .create({
        parentId,
        lessonIds,
        paidDate: this.toDateOnly(raw.paidDate),
        method: raw.method || null,
        note: raw.note || null
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.messageService.add({ severity: 'success', summary: 'התשלום נרשם ונשלח אישור להורה' });
          this.paymentForm.reset({ paidDate: new Date(), method: '', note: '' });
          this.onParentChange();
          this.loadSummary();
          this.loadPayments();
        },
        error: err => {
          this.saving.set(false);
          this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'רישום התשלום נכשל.') });
        }
      });
  }

  confirmDeletePayment(payment: Payment): void {
    this.confirmationService.confirm({
      message: 'לבטל את רישום התשלום? השיעורים המכוסים יחזרו לפתוחים לתשלום.',
      header: 'אישור ביטול',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'בטל תשלום',
      rejectLabel: 'סגור',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.deletePayment(payment.id)
    });
  }

  sendMonthlyReminders(): void {
    this.sendingReminders.set(true);
    this.paymentsService.sendMonthlyReminders().subscribe({
      next: result => {
        this.sendingReminders.set(false);
        this.messageService.add({ severity: 'success', summary: `נשלחו ${result.sentCount} תזכורות תשלום` });
      },
      error: err => {
        this.sendingReminders.set(false);
        this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'שליחת התזכורות נכשלה.') });
      }
    });
  }

  private deletePayment(id: string): void {
    this.paymentsService.delete(id).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'התשלום בוטל' });
        this.loadSummary();
        this.loadPayments();
        if (this.selectedParentId()) this.onParentChange();
      },
      error: err => this.messageService.add({ severity: 'error', summary: 'שגיאה', detail: extractErrorMessage(err, 'הביטול נכשל.') })
    });
  }

  private loadPayments(): void {
    this.paymentsLoading.set(true);
    this.paymentsService.list().subscribe({
      next: payments => {
        this.payments.set(payments);
        this.paymentsLoading.set(false);
      },
      error: () => this.paymentsLoading.set(false)
    });
  }

  private toDateOnly(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
