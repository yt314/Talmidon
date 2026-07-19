import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { LESSON_STATUS_LABELS, LessonStatus } from '../../lessons/lessons.models';
import { StudentLesson } from '../student-portal.models';
import { StudentPortalService } from '../student-portal.service';

@Component({
  selector: 'app-student-lessons',
  imports: [DatePipe, TableModule, TagModule],
  templateUrl: './student-lessons.component.html'
})
export class StudentLessonsComponent implements OnInit {
  private readonly portalService = inject(StudentPortalService);

  protected readonly statusLabel = (status: LessonStatus): string => LESSON_STATUS_LABELS[status];
  protected readonly lessons = signal<StudentLesson[]>([]);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    this.portalService.mySchedule().subscribe({
      next: lessons => {
        this.lessons.set(lessons);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
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
