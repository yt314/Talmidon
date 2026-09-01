import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { PageHeaderComponent } from '../../../shared/ui/page-header.component';
import { LESSON_STATUS_LABELS, LESSON_STATUS_SEVERITY, Lesson, LessonStatus } from '../../lessons/lessons.models';
import { OpenCharge } from '../../payments/payments.models';
import { MyChild } from '../parent-portal.models';
import { ParentPortalService } from '../parent-portal.service';

@Component({
  selector: 'app-parent-dashboard',
  imports: [RouterLink, DatePipe, CardModule, TagModule, PageHeaderComponent],
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
    if (!lessons) return null;
    const now = new Date();
    return lessons
      .filter(l => (l.status === LessonStatus.Scheduled || l.status === LessonStatus.Requested) && new Date(l.startTime) >= now)
      .sort((a, b) => new Date(a.startTime).getTime() - new Date(b.startTime).getTime())
      .slice(0, 5);
  }

  protected nextLesson(): Lesson | null {
    return this.upcomingLessons()?.[0] ?? null;
  }
}
