
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
import { ContactRequestsService } from '../contact-requests/contact-requests.service';
import { PaymentsService } from '../payments/payments.service';
import { StudentsService } from '../students/students.service';
import { ProfileSetupService } from '../teacher/profile-setup/profile-setup.service';
import { IsraelDatePipe } from '../../core/i18n/israel-date.pipe';

@Component({
  selector: 'app-dashboard',
  imports: [ RouterLink,
    ButtonModule,
    CardModule,
    TagModule,
    EmptyStateComponent,
    PageHeaderComponent,
    StatCardComponent,
    SpotlightDirective, IsraelDatePipe],
  templateUrl: './dashboard.component.html'
})
export class DashboardComponent implements OnInit {
  private readonly lessonsService = inject(LessonsService);
  private readonly paymentsService = inject(PaymentsService);
  private readonly contactRequestsService = inject(ContactRequestsService);
  private readonly studentsService = inject(StudentsService);
  private readonly profileSetup = inject(ProfileSetupService);

  protected readonly statusLabel = (status: LessonStatus): string => LESSON_STATUS_LABELS[status];
  protected readonly statusSeverity = (status: LessonStatus) => LESSON_STATUS_SEVERITY[status];

  protected readonly todayLessons = signal<Lesson[] | null>(null);
  protected readonly pendingLessonRequests = signal<number | null>(null);
  protected readonly pendingChangeRequests = signal<number | null>(null);
  protected readonly openCharges = signal<OpenCharge[] | null>(null);
  protected readonly lessonsToMark = signal<number | null>(null);
  protected readonly newContactRequests = signal<number | null>(null);
  private readonly students = signal<number | null>(null);
  private readonly profileComplete = signal<boolean | null>(null);

  /**
   * שלושת הצעדים שהופכים חשבון ריק למערכת עובדת. מוצג רק כשעוד חסר משהו,
   * ונעלם מעצמו — מורה ותיקה לא אמורה לראות הדרכה בכל כניסה.
   */
  protected readonly onboarding = computed(() => {
    const students = this.students();
    const profile = this.profileComplete();
    const lessons = this.totalLessons();
    // עד שהכול נטען אין מה להציג: הבהוב של צעדים "פתוחים" הוא רעש
    if (students === null || profile === null || lessons === null) return null;

    const steps = [
      { label: 'להשלים את הפרופיל הציבורי', hint: 'כדי שהורים ימצאו אותך', link: '/app/profile', done: profile },
      { label: 'להוסיף תלמיד ראשון', hint: 'משם נפתחים היומן והחיובים', link: '/app/students', done: students > 0 },
      { label: 'לקבוע שיעור ראשון', hint: 'ביומן, בגרירה על השעה', link: '/app/lessons', done: lessons > 0 }
    ];
    return steps.every(s => s.done) ? null : steps;
  });

  protected readonly onboardingDone = computed(() => this.onboarding()?.filter(s => s.done).length ?? 0);
  private readonly totalLessons = signal<number | null>(null);

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
    this.studentsService.list().subscribe({
      next: rows => this.students.set(rows.length),
      error: () => this.students.set(0)
    });
    this.profileSetup.load().subscribe({
      next: profile => this.profileComplete.set(profile.isProfileComplete),
      // כשל בטעינה לא אמור להציג "הפרופיל לא מלא" למי שכן מילאה אותו
      error: () => this.profileComplete.set(true)
    });
    this.lessonsService.list().subscribe({
      next: rows => this.totalLessons.set(rows.length),
      error: () => this.totalLessons.set(0)
    });
    this.contactRequestsService.newCount().subscribe({
      next: count => this.newContactRequests.set(count),
      error: () => this.newContactRequests.set(0)
    });
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
