import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { LESSON_STATUS_LABELS, LESSON_STATUS_SEVERITY, LessonStatus } from '../../lessons/lessons.models';
import { StudentLesson } from '../student-portal.models';
import { StudentPortalService } from '../student-portal.service';

@Component({
  selector: 'app-student-dashboard',
  imports: [RouterLink, DatePipe, CardModule, TagModule],
  templateUrl: './student-dashboard.component.html'
})
export class StudentDashboardComponent implements OnInit {
  private readonly portalService = inject(StudentPortalService);

  protected readonly statusLabel = (status: LessonStatus): string => LESSON_STATUS_LABELS[status];
  protected readonly statusSeverity = (status: LessonStatus) => LESSON_STATUS_SEVERITY[status];

  protected readonly lessons = signal<StudentLesson[] | null>(null);

  /** שיעורי בית נקבעים רק בסיום שיעור (T5), ולכן קיימים רק על שיעורים שהושלמו — לא על הקרוב. */
  protected readonly recentHomework = computed(() => {
    const lessons = this.lessons();
    if (!lessons) return null;
    return (
      lessons
        .filter(l => l.status === LessonStatus.Completed && l.homework)
        .sort((a, b) => new Date(b.startTime).getTime() - new Date(a.startTime).getTime())[0] ?? null
    );
  });

  ngOnInit(): void {
    this.portalService.mySchedule().subscribe({
      next: lessons => this.lessons.set(lessons),
      error: () => this.lessons.set([])
    });
  }

  /** לא computed() בכוונה: תלוי בזמן הנוכחי, לא רק בסיגנל lessons — צריך להתעדכן בכל בדיקה, לא רק כשהשיעורים משתנים. */
  protected upcomingLessons(): StudentLesson[] | null {
    const lessons = this.lessons();
    if (!lessons) return null;
    const now = new Date();
    return lessons
      .filter(l => l.status === LessonStatus.Scheduled && new Date(l.startTime) >= now)
      .sort((a, b) => new Date(a.startTime).getTime() - new Date(b.startTime).getTime())
      .slice(0, 5);
  }

  protected nextLesson(): StudentLesson | null {
    return this.upcomingLessons()?.[0] ?? null;
  }
}
