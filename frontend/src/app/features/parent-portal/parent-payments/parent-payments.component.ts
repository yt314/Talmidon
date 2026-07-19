import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { TableModule } from 'primeng/table';
import { TabsModule } from 'primeng/tabs';
import { TagModule } from 'primeng/tag';
import { OpenCharge, Payment } from '../../payments/payments.models';
import { ParentPortalService } from '../parent-portal.service';

@Component({
  selector: 'app-parent-payments',
  imports: [DatePipe, TableModule, TabsModule, TagModule],
  templateUrl: './parent-payments.component.html'
})
export class ParentPaymentsComponent implements OnInit {
  private readonly portalService = inject(ParentPortalService);

  protected readonly openCharges = signal<OpenCharge[]>([]);
  protected readonly openChargesLoading = signal(true);
  protected readonly payments = signal<Payment[]>([]);
  protected readonly paymentsLoading = signal(true);

  protected readonly openChargesTotal = computed(() => this.openCharges().reduce((sum, c) => sum + c.amount, 0));

  ngOnInit(): void {
    this.portalService.myOpenCharges().subscribe({
      next: charges => {
        this.openCharges.set(charges);
        this.openChargesLoading.set(false);
      },
      error: () => this.openChargesLoading.set(false)
    });
    this.portalService.myPayments().subscribe({
      next: payments => {
        this.payments.set(payments);
        this.paymentsLoading.set(false);
      },
      error: () => this.paymentsLoading.set(false)
    });
  }
}
