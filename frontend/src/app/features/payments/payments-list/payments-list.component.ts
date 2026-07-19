import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TabsModule } from 'primeng/tabs';
import { TagModule } from 'primeng/tag';
import { extractErrorMessage } from '../../../core/http/extract-error-message';
import { Parent } from '../../parents/parents.models';
import { ParentsService } from '../../parents/parents.service';
import { OpenCharge, Payment } from '../payments.models';
import { PaymentsService } from '../payments.service';

@Component({
  selector: 'app-payments-list',
  imports: [
    ReactiveFormsModule,
    FormsModule,
    DatePipe,
    ButtonModule,
    DatePickerModule,
    InputTextModule,
    SelectModule,
    TableModule,
    TabsModule,
    TagModule
  ],
  templateUrl: './payments-list.component.html'
})
export class PaymentsListComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly paymentsService = inject(PaymentsService);
  private readonly parentsService = inject(ParentsService);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly parents = signal<Parent[]>([]);
  protected readonly selectedParentId = signal<string | null>(null);
  protected readonly openCharges = signal<OpenCharge[]>([]);
  protected readonly openChargesLoading = signal(false);
  protected readonly selectedLessonIds = signal<Set<string>>(new Set());
  protected readonly saving = signal(false);
  protected readonly sendingReminders = signal(false);

  protected readonly payments = signal<Payment[]>([]);
  protected readonly paymentsLoading = signal(true);

  protected readonly selectedTotal = computed(() => {
    const ids = this.selectedLessonIds();
    return this.openCharges()
      .filter(c => ids.has(c.lessonId))
      .reduce((sum, c) => sum + c.amount, 0);
  });

  protected readonly paymentForm = this.fb.nonNullable.group({
    paidDate: this.fb.nonNullable.control<Date>(new Date(), Validators.required),
    method: [''],
    note: ['']
  });

  ngOnInit(): void {
    this.parentsService.list().subscribe(parents => this.parents.set(parents));
    this.loadPayments();
  }

  onParentChange(): void {
    this.selectedLessonIds.set(new Set());
    const parentId = this.selectedParentId();
    if (!parentId) {
      this.openCharges.set([]);
      return;
    }
    this.openChargesLoading.set(true);
    this.paymentsService.openCharges(parentId).subscribe({
      next: charges => {
        this.openCharges.set(charges);
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
  }

  toggleAll(): void {
    const all = this.openCharges();
    this.selectedLessonIds.set(this.selectedLessonIds().size === all.length ? new Set() : new Set(all.map(c => c.lessonId)));
  }

  markAsPaid(): void {
    const parentId = this.selectedParentId();
    const lessonIds = Array.from(this.selectedLessonIds());
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
