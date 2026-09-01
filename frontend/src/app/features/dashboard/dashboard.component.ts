import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { SpotlightDirective } from '../../shared/ui/spotlight.directive';
import { StatCardComponent } from '../../shared/ui/stat-card.component';
import { OpenCharge } from '../payments/payments.models';
import { LESSON_STATUS_LABELS, LESSON_STATUS_SEVERITY, ChangeRequestStatus, Lesson, LessonStatus } from '../lessons/lessons.models';
import { LessonsService } from '../lessons/lessons.service';
import { PaymentsService } from '../payments/payments.service';

@Component({
  selector: 'app-dashboard',
  imports: [
    RouterLink,
    DatePipe,
    ButtonModule,
    CardModule,
    TagModule,
    EmptyStateComponent,
    PageHeaderComponent,
    StatCardComponent,
    SpotlightDirective
  ],
  templateUrl: './dashboard.component.html'
})
export class DashboardComponent implements OnInit {
  private readonly lessonsService = inject(LessonsService);
  private readonly paymentsService = inject(PaymentsService);

  protected readonly statusLabel = (status: LessonStatus): string => LESSON_STATUS_LABELS[status];
  protected readonly statusSeverity = (status: LessonStatus) => LESSON_STATUS_SEVERITY[status];

  protected readonly todayLessons = signal<Lesson[] | null>(null);
  protected readonly pendingLessonRequests = signal<number | null>(null);
  protected readonly pendingChangeRequests = signal<number | null>(null);
  protected readonly openCharges = signal<OpenCharge[] | null>(null);
  protected readonly lessonsToMark = signal<number | null>(null);

  protected readonly pendingRequestsTotal = computed(() => {
    const a = this.pendingLessonRequests();
    const b = this.pendingChangeRequests();
    return a === null || b === null ? null : a + b;
  });

  /** שורת ההסבר בכותרת — תמונת מצב של היום במשפט אחד. */
  protected readonly todaySummary = computed(() => {
    const lessons = this.todayLessons();
    if (lessons === null) return null;
    if (lessons.length === 0) return 'אין שיעורים מתוזמנים להיום.';
    return lessons.length === 1 ? 'שיעור אחד מתוזמן להיום.' : `${lessons.length} שיעורים מתוזמנים להיום.`;
  });

  protected readonly openChargesCount = computed(() => this.openCharges()?.length ?? null);
  protected readonly openChargesTotal = computed(
    () => this.openCharges()?.reduce((sum, c) => sum + c.amount, 0) ?? null
  );

  ngOnInit(): void {
    const startOfDay = new Date();
    startOfDay.setHours(0, 0, 0, 0);
    const endOfDay = new Date();
    endOfDay.setHours(23, 59, 59, 999);

    this.lessonsService.list(startOfDay, endOfDay).subscribe({
      next: lessons => this.todayLessons.set(lessons),
      error: () => this.todayLessons.set([])
    });
    this.lessonsService.list(undefined, undefined, LessonStatus.Requested).subscribe({
      next: lessons => this.pendingLessonRequests.set(lessons.length),
      error: () => this.pendingLessonRequests.set(0)
    });
    this.lessonsService.listChangeRequests(ChangeRequestStatus.Pending).subscribe({
      next: requests => this.pendingChangeRequests.set(requests.length),
      error: () => this.pendingChangeRequests.set(0)
    });
    this.paymentsService.openCharges().subscribe({
      next: charges => this.openCharges.set(charges),
      error: () => this.openCharges.set([])
    });

    // שיעורים שהמועד שלהם עבר ועדיין "מתוזמן" — ממתינים לסימום
    const now = new Date();
    this.lessonsService.list(undefined, now, LessonStatus.Scheduled).subscribe({
      next: lessons => this.lessonsToMark.set(lessons.filter(l => new Date(l.endTime).getTime() < now.getTime()).length),
      error: () => this.lessonsToMark.set(0)
    });
  }
}
