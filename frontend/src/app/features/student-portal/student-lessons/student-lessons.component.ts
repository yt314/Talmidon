import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { EventInput } from 'fullcalendar';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { TagModule } from 'primeng/tag';
import { PageHeaderComponent } from '../../../shared/ui/page-header.component';
import { CalendarEventExtendedProps } from '../../../shared/calendar/lesson-calendar.model';
import { LessonCalendarComponent } from '../../../shared/calendar/lesson-calendar.component';
import { LESSON_STATUS_COLOR, LESSON_STATUS_LABELS, LESSON_STATUS_SEVERITY, LessonStatus } from '../../lessons/lessons.models';
import { StudentLesson } from '../student-portal.models';
import { StudentPortalService } from '../student-portal.service';

@Component({
  selector: 'app-student-lessons',
  imports: [DatePipe, ButtonModule, DialogModule, TagModule, LessonCalendarComponent, PageHeaderComponent],
  templateUrl: './student-lessons.component.html'
})
export class StudentLessonsComponent implements OnInit {
  private readonly portalService = inject(StudentPortalService);

  protected readonly statusLabel = (status: LessonStatus): string => LESSON_STATUS_LABELS[status];
  protected readonly statusSeverity = (status: LessonStatus) => LESSON_STATUS_SEVERITY[status];
  protected readonly lessons = signal<StudentLesson[]>([]);
  protected readonly loading = signal(true);

  protected readonly showLessonDetailDialog = signal(false);
  protected readonly selectedLesson = signal<StudentLesson | null>(null);

  protected readonly calendarEvents = computed<EventInput[]>(() =>
    this.lessons().map(lesson => {
      const color = LESSON_STATUS_COLOR[lesson.status];
      const extendedProps: CalendarEventExtendedProps = { kind: 'lesson', refId: lesson.id };
      return {
        id: lesson.id,
        title: this.statusLabel(lesson.status),
        start: lesson.startTime,
        end: lesson.endTime,
        color: color.color,
        contrastColor: color.contrastColor,
        classNames: lesson.status === LessonStatus.Cancelled ? ['lesson-cal-muted'] : [],
        extendedProps
      };
    })
  );

  ngOnInit(): void {
    this.portalService.mySchedule().subscribe({
      next: lessons => {
        this.lessons.set(lessons);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  onEventClicked(props: CalendarEventExtendedProps): void {
    const lesson = this.lessons().find(l => l.id === props.refId);
    if (!lesson) return;
    this.selectedLesson.set(lesson);
    this.showLessonDetailDialog.set(true);
  }
}
