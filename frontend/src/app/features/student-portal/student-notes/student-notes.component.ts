import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { SkeletonModule } from 'primeng/skeleton';
import { EmptyStateComponent } from '../../../shared/ui/empty-state.component';
import { PageHeaderComponent } from '../../../shared/ui/page-header.component';
import { StudentNote } from '../student-portal.models';
import { StudentPortalService } from '../student-portal.service';

@Component({
  selector: 'app-student-notes',
  imports: [DatePipe, ButtonModule, SkeletonModule, PageHeaderComponent, EmptyStateComponent],
  templateUrl: './student-notes.component.html'
})
export class StudentNotesComponent implements OnInit {
  private readonly portalService = inject(StudentPortalService);
  private readonly router = inject(Router);

  protected readonly notes = signal<StudentNote[]>([]);
  protected readonly loading = signal(true);

  /**
   * תשובה על הערה היא שיחה רגילה, ולכן היא נפתחת במסך ההודעות — עם מזהה ההערה, כדי
   * שהמורה תראה על מה מגיבים ולא רק שהגיעה הודעה.
   */
  protected reply(note: StudentNote): void {
    this.router.navigate(['/student/messages'], {
      queryParams: { noteId: note.id, subject: 'תגובה להערה' }
    });
  }

  ngOnInit(): void {
    this.portalService.myNotes().subscribe({
      next: notes => {
        this.notes.set(notes);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
