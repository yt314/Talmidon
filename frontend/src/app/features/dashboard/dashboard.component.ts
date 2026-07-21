import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { LESSON_STATUS_LABELS, ChangeRequestStatus, Lesson, LessonStatus } from '../lessons/lessons.models';
import { LessonsService } from '../lessons/lessons.service';
import { PaymentsService } from '../payments/payments.service';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, DatePipe, CardModule, TagModule],
  templateUrl: './dashboard.component.html'
})
export class DashboardComponent implements OnInit {
  private readonly lessonsService = inject(LessonsService);
  private readonly paymentsService = inject(PaymentsService);

  protected readonly statusLabel = (status: LessonStatus): string => LESSON_STATUS_LABELS[status];

  protected readonly todayLessons = signal<Lesson[] | null>(null);
  protected readonly pendingLessonRequests = signal<number | null>(null);
  protected readonly pendingChangeRequests = signal<number | null>(null);
  protected readonly openChargesTotal = signal<number | null>(null);
  protected readonly openChargesCount = signal<number | null>(null);

  protected readonly pendingRequestsTotal = computed(() => {
    const a = this.pendingLessonRequests();
    const b = this.pendingChangeRequests();
    return a === null || b === null ? null : a + b;
  });

  ngOnInit(): void {
    const startOfDay = new Date();
    startOfDay.setHours(0, 0, 0, 0);
    const endOfDay = new Date();
    endOfDay.setHours(23, 59, 59, 999);

    this.lessonsService.list(startOfDay, endOfDay).subscribe(lessons => this.todayLessons.set(lessons));
    this.lessonsService.list(undefined, undefined, LessonStatus.Requested).subscribe(lessons => this.pendingLessonRequests.set(lessons.length));
    this.lessonsService
      .listChangeRequests(ChangeRequestStatus.Pending)
      .subscribe(requests => this.pendingChangeRequests.set(requests.length));
    this.paymentsService.openCharges().subscribe(charges => {
      this.openChargesCount.set(charges.length);
      this.openChargesTotal.set(charges.reduce((sum, c) => sum + c.amount, 0));
    });
  }

  protected statusSeverity(status: LessonStatus): 'success' | 'info' | 'warn' | 'danger' | 'secondary' {
    switch (status) {
      case LessonStatus.Completed:
        return 'success';
      case LessonStatus.Scheduled:
        return 'info';
      case LessonStatus.Requested:
        return 'warn';
      case LessonStatus.Cancelled:
      case LessonStatus.Declined:
        return 'danger';
      default:
        return 'secondary';
    }
  }
}
