
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { StatCardComponent } from '../../../shared/ui/stat-card.component';
import { PageHeaderComponent } from '../../../shared/ui/page-header.component';
import { LESSON_STATUS_LABELS, LESSON_STATUS_SEVERITY, LessonStatus } from '../../lessons/lessons.models';
import { StudentLesson } from '../student-portal.models';
import { StudentPortalService } from '../student-portal.service';
import { IsraelDatePipe } from '../../../core/i18n/israel-date.pipe';

@Component({
  selector: 'app-student-dashboard',
  imports: [ CardModule, TagModule, PageHeaderComponent, StatCardComponent, IsraelDatePipe],
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

  /**
   * לא computed() בכוונה: תלוי בזמן הנוכחי, לא רק בסיגנל lessons — צריך להתעדכן בכל בדיקה, לא רק כשהשיעורים משתנים.
   *
   * בקשה שממתינה לאישור נכללת כאן, כמו אצל ההורה. תלמידה יכולה לבקש שיעור
   * מהיומן שלה, ובלי זה המסך היה עונה לה מיד "אין שיעורים קרובים" על בקשה
   * שהיא בדיוק שלחה. התגית לצד השורה מבדילה בין "מתוזמן" ל"ממתין לאישור".
   */
  protected upcomingLessons(): StudentLesson[] | null {
    const lessons = this.lessons();
    if (!lessons) return null;
    const now = new Date();
    return lessons
      .filter(
        l =>
          (l.status === LessonStatus.Scheduled || l.status === LessonStatus.Requested) &&
          new Date(l.startTime) >= now
      )
      .sort((a, b) => new Date(a.startTime).getTime() - new Date(b.startTime).getTime())
      .slice(0, 5);
  }

  protected nextLesson(): StudentLesson | null {
    return this.upcomingLessons()?.[0] ?? null;
  }

  /** מתחת לשעה של השיעור הבא: אם הוא עדיין בקשה, שלא ייראה כאילו הוא סגור. */
  protected nextLessonHint(): string | null {
    const next = this.nextLesson();
    if (next) return next.status === LessonStatus.Requested ? 'ממתין לאישור המורה' : null;
    return this.lessons() === null ? null : 'אין שיעורים קרובים';
  }
}
