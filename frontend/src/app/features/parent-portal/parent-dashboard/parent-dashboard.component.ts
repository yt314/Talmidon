
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { StatCardComponent } from '../../../shared/ui/stat-card.component';
import { PageHeaderComponent } from '../../../shared/ui/page-header.component';
import { LESSON_STATUS_LABELS, LESSON_STATUS_SEVERITY, Lesson, LessonStatus } from '../../lessons/lessons.models';
import { declinedPortalRequests, upcomingPortalLessons } from '../../lessons/portal-lessons.util';
import { OpenCharge } from '../../payments/payments.models';
import { MyChild } from '../parent-portal.models';
import { ParentPortalService } from '../parent-portal.service';
import { IsraelDatePipe } from '../../../core/i18n/israel-date.pipe';

@Component({
  selector: 'app-parent-dashboard',
  imports: [RouterLink, ButtonModule, CardModule, TagModule, PageHeaderComponent, StatCardComponent, IsraelDatePipe],
  templateUrl: './parent-dashboard.component.html'
})
export class ParentDashboardComponent implements OnInit {
  private readonly portalService = inject(ParentPortalService);

  protected readonly statusLabel = (status: LessonStatus): string => LESSON_STATUS_LABELS[status];
  protected readonly statusSeverity = (status: LessonStatus) => LESSON_STATUS_SEVERITY[status];

  protected readonly children = signal<MyChild[] | null>(null);
  protected readonly lessons = signal<Lesson[] | null>(null);
  protected readonly openCharges = signal<OpenCharge[] | null>(null);

  protected readonly childrenNames = computed(() => this.children()?.map(c => c.fullName).join(', ') ?? null);
  protected readonly openChargesCount = computed(() => this.openCharges()?.length ?? null);
  protected readonly openChargesTotal = computed(
    () => this.openCharges()?.reduce((sum, c) => sum + c.amount, 0) ?? null
  );

  ngOnInit(): void {
    this.portalService.myChildren().subscribe({
      next: children => this.children.set(children),
      error: () => this.children.set([])
    });
    this.portalService.myLessons().subscribe({
      next: lessons => this.lessons.set(lessons),
      error: () => this.lessons.set([])
    });
    this.portalService.myOpenCharges().subscribe({
      next: charges => this.openCharges.set(charges),
      error: () => this.openCharges.set([])
    });
  }

  /** לא computed() בכוונה: תלוי בזמן הנוכחי, לא רק בסיגנל lessons — צריך להתעדכן בכל בדיקה, לא רק כשהשיעורים משתנים. */
  protected upcomingLessons(): Lesson[] | null {
    const lessons = this.lessons();
    return lessons ? upcomingPortalLessons(lessons) : null;
  }

  /** בקשות שהמורה דחתה ועוד לא עבר מועדן — אחרת הן פשוט נעלמות מהמסך בלי תשובה. */
  protected declinedRequests(): Lesson[] {
    return declinedPortalRequests(this.lessons() ?? []);
  }

  protected nextLesson(): Lesson | null {
    return this.upcomingLessons()?.[0] ?? null;
  }
}
